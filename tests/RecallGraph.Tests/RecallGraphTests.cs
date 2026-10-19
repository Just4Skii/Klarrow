using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using ClosedXML.Excel;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

namespace RecallGraph.Tests;

public sealed class RecallGraphFactory : WebApplicationFactory<Program>
{
    public string DataRoot { get; } = Path.Combine(Path.GetTempPath(), "recallgraph-tests", Guid.NewGuid().ToString("N"));
    public string SeedPath { get; } = ResolveSeedPath();
    private static string ResolveSeedPath()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        for (var i = 0; i < 10 && dir != null; i++, dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "seed", "evidence");
            if (Directory.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "seed", "evidence"));
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Storage:DataRoot", DataRoot);
        builder.UseSetting("Storage:UploadsPath", Path.Combine(DataRoot, "uploads"));
        builder.UseSetting("Storage:SeedEvidencePath", SeedPath);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5 && Directory.Exists(DataRoot); attempt++)
        {
            try { Directory.Delete(DataRoot, true); }
            catch (IOException) { Thread.Sleep(200); }
        }
    }
}

public sealed class ApiTests(RecallGraphFactory factory) : IClassFixture<RecallGraphFactory>
{
    [Fact]
    public async Task Health_and_seed_load()
    {
        var client = factory.CreateClient();
        var health = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        var healthBody = await health.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("none", healthBody.GetProperty("accessControl").GetProperty("mode").GetString());
        Assert.Contains("Local prototype mode", healthBody.GetProperty("accessControl").GetProperty("notice").GetString());

        var home = await client.GetFromJsonAsync<JsonElement>("/api/workspace/home");
        Assert.True(home.GetProperty("isDemo").GetBoolean());
        Assert.Equal("demo", home.GetProperty("mode").GetString());
        Assert.True(home.GetProperty("hasSyntheticData").GetBoolean());
        Assert.True(home.GetProperty("syntheticDocumentCount").GetInt32() >= 24);
        Assert.False(string.IsNullOrEmpty(home.GetProperty("primaryInvestigationId").GetString()));
        Assert.True(home.GetProperty("documentCount").GetInt32() >= 24);
        Assert.True(home.GetProperty("factCount").GetInt32() >= 137);
    }

    [Fact]
    public async Task Forward_trace_finds_ground_truth_affected_lots()
    {
        var client = factory.CreateClient();
        var body = new { selectedType = "raw-lot", selectedId = "RM-0427", direction = "both" };
        var res = await client.PostAsJsonAsync("/api/trace-runs", body);
        Assert.True(res.IsSuccessStatusCode);
        var trace = await res.Content.ReadFromJsonAsync<JsonElement>();
        var lots = trace.GetProperty("affectedFinishedLots").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Equal(new[] { "FL-0514", "FL-0515" }, lots.OrderBy(x => x).ToArray());
        var customers = trace.GetProperty("affectedCustomers").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("Tuckshop Arendse (Benoni)", customers);
        Assert.Contains("Groot Pretoria Foods (Pty) Ltd", customers);
        Assert.Contains("Cafe Kapabela", customers);
        Assert.DoesNotContain("FL-0516", lots);
        Assert.DoesNotContain("Mall Mart Centurion", customers);
        var gaps = trace.GetProperty("gaps").GetArrayLength();
        Assert.Equal(1, gaps);
        var mb = trace.GetProperty("massBalance").GetProperty("status").GetString();
        Assert.NotEqual("reconciled", mb);
    }

    [Fact]
    public async Task Backward_trace_reaches_raw_lot()
    {
        var client = factory.CreateClient();
        var body = new { selectedType = "finished-lot", selectedId = "FL-0514", direction = "backward" };
        var res = await client.PostAsJsonAsync("/api/trace-runs", body);
        var trace = await res.Content.ReadFromJsonAsync<JsonElement>();
        var nodeIds = trace.GetProperty("nodes").EnumerateArray().Select(x => x.GetProperty("id").GetString()).ToList();
        Assert.Contains("RM-0427", nodeIds);
        Assert.Contains("PB-2026-0912-A", nodeIds);
    }

    [Fact]
    public async Task Near_match_is_warning_not_silent_link()
    {
        var client = factory.CreateClient();
        var lots = await client.GetFromJsonAsync<JsonElement>("/api/workspace/lots?query=RM-0427");
        var suspect = lots.EnumerateArray().First(x => x.GetProperty("code").GetString() == "RM-0427");
        var near = suspect.GetProperty("nearMatches").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("RM-O427", near);
    }

    [Fact]
    public async Task Fact_confirmation_creates_audit_event()
    {
        var client = factory.CreateClient();
        var facts = await client.GetFromJsonAsync<JsonElement>("/api/workspace/facts");
        var factId = facts.EnumerateArray().First().GetProperty("id").GetString()!;
        var confirm = await client.PostAsJsonAsync($"/api/facts/{factId}/confirm", new { reviewer = "tester" });
        Assert.True(confirm.IsSuccessStatusCode);
        var confirmed = await confirm.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Confirmed", confirmed.GetProperty("reviewStatus").GetString());
        Assert.Equal("tester", confirmed.GetProperty("reviewer").GetString());
        Assert.NotNull(confirmed.GetProperty("reviewedAt").GetString());

        var audits = await client.GetFromJsonAsync<JsonElement>("/api/workspace/audits");
        Assert.Contains(audits.EnumerateArray(), x => x.GetProperty("eventType").GetString() == "fact_confirmed");
    }

    [Fact]
    public async Task Edit_marks_fact_edited_and_records_reason_in_history()
    {
        var client = factory.CreateClient();
        var facts = await client.GetFromJsonAsync<JsonElement>("/api/workspace/facts");
        var fact = facts.EnumerateArray().First();
        var factId = fact.GetProperty("id").GetString()!;
        var originalSubject = fact.GetProperty("claimSubject").GetString() ?? string.Empty;

        var patch = await client.PatchAsJsonAsync($"/api/facts/{factId}", new { claimSubject = "RM-EDITED-1", reason = "Corrected from source label photo", reviewer = "tester" });
        Assert.True(patch.IsSuccessStatusCode);
        var updated = await patch.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Edited", updated.GetProperty("reviewStatus").GetString());
        Assert.Equal("RM-EDITED-1", updated.GetProperty("claimSubject").GetString());
        Assert.Equal("Corrected from source label photo", updated.GetProperty("reviewReason").GetString());
        Assert.Equal("tester", updated.GetProperty("reviewer").GetString());

        var history = await client.GetFromJsonAsync<JsonElement>($"/api/facts/{factId}/history");
        var editEvent = history.EnumerateArray().First(x => x.GetProperty("action").GetString() == "edited");
        Assert.Equal("Corrected from source label photo", editEvent.GetProperty("reason").GetString());
        Assert.Contains(originalSubject, editEvent.GetProperty("previousValuesJson").GetString());
        Assert.Contains("RM-EDITED-1", editEvent.GetProperty("newValuesJson").GetString());
    }

    [Fact]
    public async Task Upload_rejects_hostile_path_input()
    {
        var client = factory.CreateClient();
        using var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes("bad"));
        fileContent.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data")
        {
            Name = "file",
            FileName = "..\\evil.csv"
        };
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent);
        var res = await client.PostAsync("/api/documents/upload", content);
        Assert.False(res.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Provider_adapter_handles_success_malformed_http_error_and_missing_credentials()
    {
        var options = new LlamaOptions("http://localhost:9", "key", "meta-llama/Llama-3.1-70B-Instruct", 5);

        // success
        var successHandler = new StubHandler(@"{""choices"":[{""message"":{""content"":""{\""facts\"":[{\""rawValue\"":\""lot 1\""}]}""}}]}");
        using (var http = new HttpClient(successHandler))
        {
            var provider = new OpenAiCompatibleLlamaProvider(http, options);
            var result = await provider.ExtractAsync("text", null, null, "file.txt");
            Assert.True(result.IsValid);
            Assert.Single(result.Facts);
        }

        // malformed
        var malformedHandler = new StubHandler(@"{""choices"":[{""message"":{""content"":""not-json""}}]}");
        using (var http = new HttpClient(malformedHandler))
        {
            var provider = new OpenAiCompatibleLlamaProvider(http, options);
            var result = await provider.ExtractAsync("text", null, null, "file.txt");
            Assert.False(result.IsValid);
            Assert.NotNull(result.Error);
        }

        // http error
        var errorHandler = new StubHandler("boom", HttpStatusCode.InternalServerError);
        using (var http = new HttpClient(errorHandler))
        {
            var provider = new OpenAiCompatibleLlamaProvider(http, options);
            var result = await provider.ExtractAsync("text", null, null, "file.txt");
            Assert.False(result.IsValid);
        }

        // missing credentials
        var emptyOptions = new LlamaOptions("", "", "model", 5);
        using (var http = new HttpClient())
        {
            var provider = new OpenAiCompatibleLlamaProvider(http, emptyOptions);
            Assert.False(provider.Status.IsConfigured);
            var result = await provider.ExtractAsync("text", null, null, "file.txt");
            Assert.False(result.IsValid);
        }
    }

    [Fact]
    public async Task Provider_adapter_skips_malformed_fact_rows_and_coerces_numeric_raw_values()
    {
        var options = new LlamaOptions("http://localhost:9", "key", "meta-llama/Llama-3.1-70B-Instruct", 5);
        var tolerantHandler = new StubHandler(@"{""choices"":[{""message"":{""content"":""{\""facts\"":[{\""rawValue\"":500},{\""rawValue\"":null},\""not-an-object\"",{\""rawValue\"":\""ok\""}]}""}}]}");
        using (var http = new HttpClient(tolerantHandler))
        {
            var provider = new OpenAiCompatibleLlamaProvider(http, options);
            var result = await provider.ExtractAsync("text", null, null, "file.txt");
            Assert.True(result.IsValid);
            Assert.Equal(2, result.Facts.Count);
            Assert.Equal("500", result.Facts[0].RawValue);
            Assert.Equal("ok", result.Facts[1].RawValue);
        }
    }

    [Fact]
    public async Task Trace_run_snapshot_persists()
    {
        var client = factory.CreateClient();
        var body = new { selectedType = "raw-lot", selectedId = "RM-0427", direction = "both" };
        var first = await client.PostAsJsonAsync("/api/trace-runs", body);
        var id = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetString()!;
        // run again to ensure stable distinct IDs
        var second = await client.PostAsJsonAsync("/api/trace-runs", body);
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetString()!;
        Assert.NotEqual(id, secondId);
        var fetched = await client.GetFromJsonAsync<JsonElement>($"/api/trace-runs/{id}");
        Assert.Equal(id, fetched.GetProperty("runId").GetString());
    }

    [Fact]
    public async Task Investigation_crud_persists_and_links_trace_run()
    {
        var client = factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Pilot investigation",
            reference = "INV-TEST-1",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-0427",
            description = "Test investigation",
            status = "Open",
            actor = "tester"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        var investigationId = created.GetProperty("id").GetString()!;

        var list = await client.GetFromJsonAsync<JsonElement>("/api/investigations");
        Assert.Contains(list.EnumerateArray(), x => x.GetProperty("id").GetString() == investigationId);

        var trace = await client.PostAsJsonAsync("/api/trace-runs", new { investigationId, selectedType = "raw-lot", selectedId = "RM-0427", direction = "both" });
        var traceResult = await trace.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(investigationId, traceResult.GetProperty("investigationId").GetString());

        var patch = await client.PatchAsJsonAsync($"/api/investigations/{investigationId}", new { name = "Pilot investigation", reference = "INV-TEST-1", suspectLotType = "raw-lot", suspectLotId = "RM-0427", description = "Updated", status = "Closed", actor = "tester" });
        Assert.True(patch.IsSuccessStatusCode);
        var updated = await patch.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Closed", updated.GetProperty("status").GetString());

        var audits = await client.GetFromJsonAsync<JsonElement>("/api/workspace/audits");
        Assert.Contains(audits.EnumerateArray(), x => x.GetProperty("eventType").GetString() == "investigation_created");
        Assert.Contains(audits.EnumerateArray(), x => x.GetProperty("eventType").GetString() == "investigation_updated");
        Assert.Contains(audits.EnumerateArray(), x => x.GetProperty("eventType").GetString() == "trace_run");
    }

    [Fact]
    public async Task Duplicate_upload_is_not_silently_reimported()
    {
        var client = factory.CreateClient();
        var before = (await client.GetFromJsonAsync<JsonElement>("/api/documents")).GetArrayLength();
        var contents = "ref,lot,qty\nDUP-1,RM-0427,10 kg\n";
        var first = await UploadAsync(client, "duplicate-first.csv", contents);
        Assert.True(first.IsSuccessStatusCode);
        var second = await UploadAsync(client, "duplicate-second.csv", contents);
        Assert.True(second.IsSuccessStatusCode);
        var secondResult = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(secondResult.GetProperty("isDuplicate").GetBoolean());
        Assert.Equal("duplicate", secondResult.GetProperty("status").GetString());

        var after = (await client.GetFromJsonAsync<JsonElement>("/api/documents")).GetArrayLength();
        Assert.Equal(before + 1, after);

        var audits = await client.GetFromJsonAsync<JsonElement>("/api/workspace/audits");
        Assert.Contains(audits.EnumerateArray(), x => x.GetProperty("eventType").GetString() == "document_duplicate_skipped");
    }

    [Fact]
    public async Task Export_is_safe_and_records_an_audit_event()
    {
        var client = factory.CreateClient();
        var run = await client.PostAsJsonAsync("/api/trace-runs", new { selectedType = "raw-lot", selectedId = "RM-0427", direction = "both" });
        var runId = (await run.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetString()!;

        var csv = await client.GetAsync($"/api/trace-runs/{runId}/export?format=csv&actor=exporter");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
        var csvText = await csv.Content.ReadAsStringAsync();
        Assert.Contains("Evidence", csvText);
        Assert.Contains("SYNTHETIC", csvText, StringComparison.OrdinalIgnoreCase);

        var invalid = await client.GetAsync($"/api/trace-runs/{runId}/export?format=exe");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);

        var audits = await client.GetFromJsonAsync<JsonElement>("/api/workspace/audits");
        Assert.Contains(audits.EnumerateArray(), x => x.GetProperty("eventType").GetString() == "trace_exported" && x.GetProperty("actor").GetString() == "exporter");
    }

    [Fact]
    public async Task Export_includes_audit_trail_and_disclaimer()
    {
        var client = factory.CreateClient();
        var run = await client.PostAsJsonAsync("/api/trace-runs", new { selectedType = "raw-lot", selectedId = "RM-0427", direction = "both" });
        var runId = (await run.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetString()!;

        var csvText = await (await client.GetAsync($"/api/trace-runs/{runId}/export?format=csv")).Content.ReadAsStringAsync();
        Assert.Contains("AUDIT", csvText);
        Assert.Contains("trace_run", csvText);
        Assert.Contains("SYNTHETIC DEMONSTRATION DATA / NOT FOR OPERATIONAL FOOD-SAFETY DECISIONS", csvText);
        Assert.Contains("Mode=demo", csvText);
        Assert.Contains("SyntheticEvidence=true", csvText);

        var html = await (await client.GetAsync($"/api/trace-runs/{runId}/export?format=html")).Content.ReadAsStringAsync();
        Assert.Contains("Audit trail", html);
        Assert.Contains("trace_run", html);
        Assert.Contains("SYNTHETIC DEMONSTRATION DATA / NOT FOR OPERATIONAL FOOD-SAFETY DECISIONS", html);
        Assert.Contains("Synthetic evidence", html);

        var json = await client.GetFromJsonAsync<JsonElement>($"/api/trace-runs/{runId}/export?format=json");
        var trail = json.GetProperty("auditTrail");
        Assert.True(trail.GetArrayLength() > 0);
        Assert.Contains(trail.EnumerateArray(), x => x.GetProperty("eventType").GetString() == "trace_run");
        Assert.Equal("demo", json.GetProperty("workspaceMode").GetString());
        Assert.True(json.GetProperty("containsSyntheticEvidence").GetBoolean());
        Assert.Contains("SYNTHETIC DEMONSTRATION DATA", json.GetProperty("disclaimer").GetString());
    }

    [Fact]
    public void Csv_export_neutralises_formula_injection()
    {
        Assert.StartsWith("\"'=", ExportFormatter.CsvCell("=cmd|' /C calc'!A0"));
        Assert.StartsWith("\"'@", ExportFormatter.CsvCell("@SUM(A1)"));
        Assert.Equal("\"plain,value\"", ExportFormatter.CsvCell("plain,value"));
    }

    [Fact]
    public void Provenance_notice_distinguishes_pilot_and_synthetic_workspaces()
    {
        var demo = ExportFormatter.BuildProvenanceNotice("demo", 0);
        Assert.Contains("SYNTHETIC DEMONSTRATION DATA", demo);
        Assert.Contains("NOT FOR OPERATIONAL FOOD-SAFETY DECISIONS", demo);

        var pilot = ExportFormatter.BuildProvenanceNotice("pilot", 0);
        Assert.Contains("PILOT WORKSPACE", pilot);
        Assert.Contains("NOT CERTIFIED FOR REGULATORY CLEARANCE", pilot);
        Assert.DoesNotContain("SYNTHETIC DEMONSTRATION DATA", pilot);

        var mixed = ExportFormatter.BuildProvenanceNotice("pilot", 3);
        Assert.Contains("PILOT WORKSPACE", mixed);
        Assert.Contains("3 seeded synthetic demonstration link(s)", mixed);
    }

    [Fact]
    public void Csv_and_html_exports_use_invariant_decimal_separator_regardless_of_server_culture()
    {
        var result = new TraceResultDto
        {
            RunId = Guid.NewGuid(),
            SelectedType = "raw-lot",
            SelectedId = "RM-0873",
            Direction = "both",
            WorkspaceMode = "pilot",
            CompletedAt = new DateTime(2026, 10, 17, 17, 2, 22, DateTimeKind.Utc),
            Edges =
            {
                new TraceEdgeDto
                {
                    FromType = "raw-lot", FromId = "RM-0873", ToType = "finished-lot", ToId = "PB-2026-1012-A",
                    LinkKind = "raw_to_batch", State = LinkState.Confirmed, Quantity = 180.5m, QuantityUnit = "kg"
                }
            }
        };

        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var csv = ExportFormatter.BuildCsv(result);
            Assert.Contains("\"180.5\"", csv);
            Assert.DoesNotContain("180,5", csv);

            var html = ExportFormatter.BuildHtml(result);
            Assert.Contains("180.5 kg", html);
            Assert.DoesNotContain("180,5", html);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Csv_export_bytes_include_utf8_bom_preamble()
    {
        var result = new TraceResultDto { RunId = Guid.NewGuid(), SelectedType = "raw-lot", SelectedId = "RM-0873", Direction = "both" };
        var bytes = ExportFormatter.BuildCsvBytes(result);
        Assert.True(bytes.Length > 3);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        Assert.Equal(ExportFormatter.BuildCsv(result), Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3));
    }

    [Fact]
    public async Task Facts_expose_synthetic_source_provenance()
    {
        var client = factory.CreateClient();
        var facts = await client.GetFromJsonAsync<JsonElement>("/api/workspace/facts");
        var allowed = new[] { "SeededFixture", "ImportedFile", "ManualEntry", "DemoParser" };
        Assert.All(facts.EnumerateArray(), f => Assert.Contains(f.GetProperty("sourceKind").GetString(), allowed));
        Assert.Contains(facts.EnumerateArray(), f => f.GetProperty("sourceKind").GetString() == "SeededFixture");
    }

    [Fact]
    public async Task Provider_sends_auth_header_and_honours_timeout()
    {
        var options = new LlamaOptions("http://localhost:9", "secret-key", "meta-llama/Llama-3.1-70B-Instruct", 1);
        var capturing = new CapturingHandler();
        using (var http = new HttpClient(capturing))
        {
            var provider = new OpenAiCompatibleLlamaProvider(http, options);
            await provider.ExtractAsync("text", null, null, "file.txt");
            Assert.NotNull(capturing.Authorization);
            Assert.Equal("Bearer", capturing.Authorization!.Scheme);
            Assert.Equal("secret-key", capturing.Authorization.Parameter);
        }

        var slowOptions = new LlamaOptions("http://localhost:9", "key", "model", 1);
        var slowHandler = new SlowHandler();
        using (var http = new HttpClient(slowHandler))
        {
            var provider = new OpenAiCompatibleLlamaProvider(http, slowOptions);
            var result = await provider.ExtractAsync("text", null, null, "file.txt");
            Assert.False(result.IsValid);
            Assert.NotNull(result.Error);
        }
    }

    [Fact]
    public async Task Gap_engine_reports_missing_quantity_mismatch_and_ambiguity()
    {
        var client = factory.CreateClient();
        var findings = await client.GetFromJsonAsync<JsonElement>("/api/gaps?refresh=true");
        var kinds = findings.EnumerateArray().Select(x => x.GetProperty("kind").GetString()).ToList();
        Assert.Contains("missing-downstream-evidence", kinds);
        Assert.Contains("quantity-mismatch", kinds);
        Assert.Contains("ambiguous-identifier", kinds);
        Assert.Contains("unreviewed-candidates", kinds);
        Assert.All(findings.EnumerateArray(), f => Assert.False(string.IsNullOrWhiteSpace(f.GetProperty("stableKey").GetString())));

        // rerunning must not duplicate findings
        var second = await client.GetFromJsonAsync<JsonElement>("/api/gaps?refresh=true");
        Assert.Equal(findings.GetArrayLength(), second.GetArrayLength());

        // The workspace headline count must use the same canonical source as the Findings page.
        var openFindings = second.EnumerateArray().Count(x => x.GetProperty("status").GetString() == "Open");
        var home = await client.GetFromJsonAsync<JsonElement>("/api/workspace/home");
        Assert.Equal(openFindings, home.GetProperty("openGapCount").GetInt32());
    }

    [Fact]
    public async Task Import_sets_processing_status_and_unsupported_images_are_marked()
    {
        var client = factory.CreateClient();
        var csv = await UploadAsync(client, $"status-{Guid.NewGuid():N}.csv", "ref,lot,qty\nS1,RM-0427,1 kg\n");
        var csvResult = await csv.Content.ReadFromJsonAsync<JsonElement>();
        var csvDoc = await client.GetFromJsonAsync<JsonElement>($"/api/documents/{csvResult.GetProperty("documentId").GetString()}");
        Assert.Equal("parsed", csvDoc.GetProperty("processingStatus").GetString());
        Assert.False(string.IsNullOrWhiteSpace(csvDoc.GetProperty("parserName").GetString()));

        using var form = new MultipartFormDataContent();
        var image = new ByteArrayContent(Encoding.UTF8.GetBytes("not-a-real-image"));
        image.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "file", FileName = "label.png" };
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(image);
        var imageResponse = await client.PostAsync("/api/documents/upload", form);
        var imageResult = await imageResponse.Content.ReadFromJsonAsync<JsonElement>();
        var imageDoc = await client.GetFromJsonAsync<JsonElement>($"/api/documents/{imageResult.GetProperty("documentId").GetString()}");
        Assert.Equal("retained_not_ocr_processed", imageDoc.GetProperty("processingStatus").GetString());
    }

    [Fact]
    public async Task Match_proposals_require_human_review_and_create_aliases_on_confirmation()
    {
        var client = factory.CreateClient();
        var proposals = await client.GetFromJsonAsync<JsonElement>("/api/match-proposals?refresh=true");
        var oZero = proposals.EnumerateArray().First(x => x.GetProperty("leftValue").GetString() == "RM-0427" && x.GetProperty("rightValue").GetString() == "RM-O427");
        Assert.Equal("Candidate", oZero.GetProperty("state").GetString());
        Assert.Equal("heuristic-not-calibrated", oZero.GetProperty("scoreKind").GetString());
        Assert.DoesNotContain("auto", oZero.GetProperty("state").GetString(), StringComparison.OrdinalIgnoreCase);

        // candidate match must not create an alias before review
        var beforeAliases = await client.GetFromJsonAsync<JsonElement>("/api/entity-aliases");
        Assert.DoesNotContain(beforeAliases.EnumerateArray(), x => x.GetProperty("aliasValue").GetString() == "RM-O427");

        var id = oZero.GetProperty("id").GetString()!;
        var confirm = await client.PostAsJsonAsync($"/api/match-proposals/{id}/confirm", new { actor = "reviewer", reason = "label photo confirmed the zero" });
        Assert.True(confirm.IsSuccessStatusCode);
        var confirmed = await confirm.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Confirmed", confirmed.GetProperty("state").GetString());

        var aliases = await client.GetFromJsonAsync<JsonElement>("/api/entity-aliases");
        Assert.Contains(aliases.EnumerateArray(), x => x.GetProperty("aliasValue").GetString() == "RM-O427" && x.GetProperty("canonicalValue").GetString() == "RM-0427");

        var audits = await client.GetFromJsonAsync<JsonElement>("/api/workspace/audits");
        Assert.Contains(audits.EnumerateArray(), x => x.GetProperty("eventType").GetString() == "match_confirmed");
    }

    [Fact]
    public async Task Fact_history_preserves_review_events()
    {
        var client = factory.CreateClient();
        var facts = await client.GetFromJsonAsync<JsonElement>("/api/workspace/facts");
        var factId = facts.EnumerateArray().First(x => x.GetProperty("reviewStatus").GetString() == "Unreviewed").GetProperty("id").GetString()!;
        var confirm = await client.PostAsJsonAsync($"/api/facts/{factId}/confirm", new { reviewer = "history-tester", notes = "checked against source" });
        Assert.True(confirm.IsSuccessStatusCode);
        var history = await client.GetFromJsonAsync<JsonElement>($"/api/facts/{factId}/history");
        Assert.NotEqual(0, history.GetArrayLength());
        Assert.Contains(history.EnumerateArray(), x => x.GetProperty("action").GetString() == "confirmed" && x.GetProperty("reason").GetString() == "checked against source");
    }

    [Fact]
    public async Task Investigation_evidence_scope_prevents_cross_case_leakage()
    {
        var client = factory.CreateClient();
        var investigations = await client.GetFromJsonAsync<JsonElement>("/api/investigations");
        var demoId = investigations.EnumerateArray().First(x => x.GetProperty("reference").GetString() == "DEMO-2026-09").GetProperty("id").GetString()!;

        var create = await client.PostAsJsonAsync("/api/investigations", new { name = "Scope case", reference = "SCOPE-1", suspectLotType = "raw-lot", suspectLotId = "RM-0100", description = "scope test", status = "Open", actor = "tester" });
        var newId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(newId), "investigationId");
        var csv = $"ref,lot,qty\nSCOPE-{Guid.NewGuid():N},RM-0100,5 kg\n";
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "file", FileName = $"scope-{Guid.NewGuid():N}.csv" };
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file);
        var upload = await client.PostAsync("/api/documents/upload", form);
        Assert.True(upload.IsSuccessStatusCode);
        var uploadedId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("documentId").GetString()!;

        var scoped = await client.GetFromJsonAsync<JsonElement>($"/api/documents?investigationId={newId}");
        Assert.Contains(scoped.EnumerateArray(), x => x.GetProperty("id").GetString() == uploadedId);

        var demoDocs = await client.GetFromJsonAsync<JsonElement>($"/api/documents?investigationId={demoId}");
        Assert.DoesNotContain(demoDocs.EnumerateArray(), x => x.GetProperty("id").GetString() == uploadedId);

        var demoFacts = await client.GetFromJsonAsync<JsonElement>($"/api/workspace/facts?investigationId={demoId}");
        Assert.DoesNotContain(demoFacts.EnumerateArray(), x => x.GetProperty("sourceDocumentId").GetString() == uploadedId);

        var demoDocCount = demoDocs.GetArrayLength();
        Assert.True(demoDocCount >= 24);

        // Demo investigation still traces its assigned seeded links.
        var demoTrace = await client.PostAsJsonAsync("/api/trace-runs", new { investigationId = demoId, selectedType = "raw-lot", selectedId = "RM-0427", direction = "both" });
        var demoResult = await demoTrace.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("FL-0514", demoResult.GetProperty("affectedFinishedLots").EnumerateArray().Select(x => x.GetString()));
        Assert.Contains("FL-0515", demoResult.GetProperty("affectedFinishedLots").EnumerateArray().Select(x => x.GetString()));

        // A different investigation must not see the demo links.
        var scopedTrace = await client.PostAsJsonAsync("/api/trace-runs", new { investigationId = newId, selectedType = "raw-lot", selectedId = "RM-0427", direction = "both" });
        var scopedResult = await scopedTrace.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, scopedResult.GetProperty("affectedFinishedLots").GetArrayLength());
    }

    [Fact]
    public async Task Source_content_resolves_line_locators_and_enforces_scope_and_root_containment()
    {
        var client = factory.CreateClient();
        var create = await client.PostAsJsonAsync("/api/investigations", new { name = "Content case", reference = "CONTENT-1", suspectLotType = "raw-lot", suspectLotId = "RM-0200", description = "content test", status = "Open", actor = "tester" });
        var investigationId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var scoped = await UploadScopedAsync(client, investigationId, $"content-{Guid.NewGuid():N}.csv", "ref,lot,qty\nC1,RM-0200,5 kg\n");
        Assert.True(scoped.IsSuccessStatusCode);
        var scopedDocId = (await scoped.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("documentId").GetString()!;

        var resolved = await client.GetFromJsonAsync<JsonElement>($"/api/documents/{scopedDocId}/content?investigationId={investigationId}&locator={Uri.EscapeDataString("lines 1-1")}");
        Assert.Equal("resolved", resolved.GetProperty("status").GetString());
        Assert.True(resolved.GetProperty("locationResolved").GetBoolean());
        Assert.True(resolved.GetProperty("lines").GetArrayLength() > 0);

        var unsupported = await client.GetFromJsonAsync<JsonElement>($"/api/documents/{scopedDocId}/content?investigationId={investigationId}&locator={Uri.EscapeDataString("page 1")}");
        Assert.Equal("unsupported_locator", unsupported.GetProperty("status").GetString());

        var invalid = await client.GetFromJsonAsync<JsonElement>($"/api/documents/{scopedDocId}/content?investigationId={investigationId}&locator={Uri.EscapeDataString("lines 99999-99999")}");
        Assert.Equal("invalid_locator", invalid.GetProperty("status").GetString());

        var missing = await client.GetAsync($"/api/documents/{Guid.NewGuid()}/content?locator={Uri.EscapeDataString("lines 1-1")}");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        // Cross-investigation: the demo investigation cannot preview another case's document.
        var investigations = await client.GetFromJsonAsync<JsonElement>("/api/investigations");
        var demoId = investigations.EnumerateArray().First(x => x.GetProperty("reference").GetString() == "DEMO-2026-09").GetProperty("id").GetString()!;
        var crossScope = await client.GetAsync($"/api/documents/{scopedDocId}/content?investigationId={demoId}&locator={Uri.EscapeDataString("lines 1-1")}");
        Assert.Equal(HttpStatusCode.NotFound, crossScope.StatusCode);

        // Null-scoped legacy document is not implicitly included in an explicit investigation scope.
        var legacy = await UploadAsync(client, $"legacy-{Guid.NewGuid():N}.csv", "ref,lot,qty\nL1,RM-0300,1 kg\n");
        var legacyDocId = (await legacy.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("documentId").GetString()!;
        var legacyScoped = await client.GetAsync($"/api/documents/{legacyDocId}/content?investigationId={demoId}&locator={Uri.EscapeDataString("lines 1-1")}");
        Assert.Equal(HttpStatusCode.NotFound, legacyScoped.StatusCode);

        // A stored path outside the derived root must not be followed.
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecallGraphDbContext>();
            var doc = await db.SourceDocuments.FirstAsync(x => x.Id == Guid.Parse(legacyDocId));
            var secretPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"recallgraph-secret-{Guid.NewGuid():N}.txt");
            await System.IO.File.WriteAllTextAsync(secretPath, "SECRET-OUTSIDE-ROOT");
            doc.DerivedTextPath = secretPath;
            await db.SaveChangesAsync();
            var expected = System.IO.Path.GetFullPath(System.IO.Path.Combine(factory.DataRoot, "uploads", "derived", $"{doc.Id:N}.txt"));
            if (System.IO.File.Exists(expected)) System.IO.File.Delete(expected);
            var safe = await client.GetFromJsonAsync<JsonElement>($"/api/documents/{legacyDocId}/content?locator={Uri.EscapeDataString("lines 1-1")}");
            Assert.Equal("content_missing", safe.GetProperty("status").GetString());
            System.IO.File.Delete(secretPath);
        }
    }

    private static async Task<HttpResponseMessage> UploadScopedAsync(HttpClient client, string investigationId, string fileName, string contents)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(investigationId), "investigationId");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(contents));
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "file", FileName = fileName };
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file);
        return await client.PostAsync("/api/documents/upload", form);
    }

    private static async Task<HttpResponseMessage> UploadAsync(HttpClient client, string fileName, string contents)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(contents));
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "file", FileName = fileName };
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file);
        return await client.PostAsync("/api/documents/upload", form);
    }

    [Fact]
    public async Task Document_to_trace_pipeline_generates_proposals_and_establishes_scope_upon_confirmation()
    {
        var client = factory.CreateClient();

        // 1. Create a dedicated investigation with a distinct suspect lot
        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Acceptance Test Case",
            reference = "ACC-2026-01",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-9901",
            description = "End-to-end evidence pipeline test",
            status = "Open",
            actor = "lead-investigator"
        });
        Assert.True(invResp.IsSuccessStatusCode);
        var inv = await invResp.Content.ReadFromJsonAsync<JsonElement>();
        var invId = inv.GetProperty("id").GetString()!;

        // 2. Upload documents covering the 4 traceability stages
        var d1 = await UploadScopedAsync(client, invId, "01_supplier_delivery.csv",
            "delivery_ref,supplier,lot,qty,unit\nDEL-801,Acme Spice Imports,RM-9901,1500,kg\n");
        Assert.True(d1.IsSuccessStatusCode);

        var d2 = await UploadScopedAsync(client, invId, "02_batch_production.csv",
            "work_order,raw_lot,production_batch,qty,unit\nWO-501,RM-9901,PB-9901,1500,kg\n");
        Assert.True(d2.IsSuccessStatusCode);

        var d3 = await UploadScopedAsync(client, invId, "03_packaging_finished.csv",
            "pack_log,production_batch,finished_lot,qty,unit\nPKG-301,PB-9901,FL-9901,1500,kg\n");
        Assert.True(d3.IsSuccessStatusCode);

        var d4 = await UploadScopedAsync(client, invId, "04_dispatch_distribution.csv",
            "dispatch_id,finished_lot,customer,qty,unit\nDSP-101,FL-9901,Metro Wholesale,1500,kg\n");
        Assert.True(d4.IsSuccessStatusCode);

        // 3. Generate relationship proposals from the uploaded evidence
        var genResp = await client.PostAsJsonAsync($"/api/relationship-proposals/generate?investigationId={invId}", new { });
        Assert.True(genResp.IsSuccessStatusCode);
        var genResult = await genResp.Content.ReadFromJsonAsync<JsonElement>();
        var generatedCount = genResult.GetProperty("generatedCount").GetInt32();
        Assert.True(generatedCount >= 3, $"Expected at least 3 generated proposals, got {generatedCount}");

        var proposals = genResult.GetProperty("proposals").EnumerateArray().ToList();
        // All proposals must start as Candidate
        Assert.All(proposals, p => Assert.Equal("Candidate", p.GetProperty("state").GetString()));

        // 4. Run trace BEFORE confirming proposals:
        // Candidate links must NOT appear in AffectedFinishedLots or AffectedCustomers!
        var preTraceResp = await client.PostAsJsonAsync("/api/trace-runs", new
        {
            investigationId = Guid.Parse(invId),
            selectedType = "raw-lot",
            selectedId = "RM-9901",
            direction = "both",
            reviewer = "analyst"
        });
        Assert.True(preTraceResp.IsSuccessStatusCode);
        var preTrace = await preTraceResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(preTrace.GetProperty("confirmedScopeEstablished").GetBoolean());
        Assert.Empty(preTrace.GetProperty("affectedFinishedLots").EnumerateArray());
        Assert.Empty(preTrace.GetProperty("affectedCustomers").EnumerateArray());

        // But they MUST appear in Possible scope
        var possibleFinished = preTrace.GetProperty("possibleFinishedLots").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("FL-9901", possibleFinished);
        var possibleCustomers = preTrace.GetProperty("possibleCustomers").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("Metro Wholesale", possibleCustomers);

        // 5. Confirm the proposals
        foreach (var p in proposals)
        {
            var pId = p.GetProperty("id").GetString()!;
            var confResp = await client.PostAsJsonAsync($"/api/relationship-proposals/{pId}/confirm", new
            {
                reviewer = "qa-lead",
                notes = "Verified against factory log sheets"
            });
            Assert.True(confResp.IsSuccessStatusCode);
        }

        // 6. Rerun trace AFTER confirmation:
        // Scope is now established and confirmed!
        var postTraceResp = await client.PostAsJsonAsync("/api/trace-runs", new
        {
            investigationId = Guid.Parse(invId),
            selectedType = "raw-lot",
            selectedId = "RM-9901",
            direction = "both",
            reviewer = "qa-lead"
        });
        Assert.True(postTraceResp.IsSuccessStatusCode);
        var postTrace = await postTraceResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(postTrace.GetProperty("confirmedScopeEstablished").GetBoolean());
        var confirmedFinished = postTrace.GetProperty("affectedFinishedLots").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("FL-9901", confirmedFinished);
        var confirmedCustomers = postTrace.GetProperty("affectedCustomers").EnumerateArray().Select(x => x.GetString()).ToList();
        Assert.Contains("Metro Wholesale", confirmedCustomers);
        Assert.Empty(postTrace.GetProperty("possibleFinishedLots").EnumerateArray());

        // Summary wording must not overstate inferred links as reviewer-verified
        var summary = postTrace.GetProperty("summary").GetString()!;
        Assert.DoesNotContain("reviewer-verified", summary);
        Assert.Contains("inferred", summary);
    }

    [Fact]
    public async Task Rejecting_supporting_fact_invalidates_and_demotes_confirmed_trace_link()
    {
        var client = factory.CreateClient();

        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Fact Demotion Test",
            reference = "DEMOTE-01",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-DEMO-01",
            description = "Test demotion on fact rejection",
            status = "Open",
            actor = "tester"
        });
        var invId = (await invResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        await UploadScopedAsync(client, invId, "batch_record.csv",
            "line,raw_lot,production_batch,qty\n1,RM-DEMO-01,PB-DEMO-01,500 kg\n");

        var genResp = await client.PostAsJsonAsync($"/api/relationship-proposals/generate?investigationId={invId}", new { });
        var genResult = await genResp.Content.ReadFromJsonAsync<JsonElement>();
        var proposal = genResult.GetProperty("proposals").EnumerateArray().First(p => p.GetProperty("fromId").GetString() == "RM-DEMO-01");
        var propId = proposal.GetProperty("id").GetString()!;

        // Confirm link
        var confResp = await client.PostAsJsonAsync($"/api/relationship-proposals/{propId}/confirm", new { reviewer = "lead", notes = "initially accepted" });
        Assert.True(confResp.IsSuccessStatusCode);

        // Find supporting fact
        var facts = await client.GetFromJsonAsync<JsonElement>($"/api/facts?investigationId={invId}");
        var fact = facts.EnumerateArray().First(f => f.GetProperty("rawValue").GetString()!.Contains("RM-DEMO-01"));
        var factId = fact.GetProperty("id").GetString()!;

        // Reject the supporting fact
        var rejResp = await client.PostAsJsonAsync($"/api/facts/{factId}/reject", new { reviewer = "auditor", notes = "batch slip was cancelled" });
        Assert.True(rejResp.IsSuccessStatusCode);

        // Verify the trace link is demoted to Candidate and marked NeedsReReview
        var links = await client.GetFromJsonAsync<JsonElement>($"/api/relationship-proposals?investigationId={invId}");
        var demotedLink = links.EnumerateArray().First(l => l.GetProperty("id").GetString() == propId);
        Assert.Equal("Candidate", demotedLink.GetProperty("state").GetString());
        Assert.True(demotedLink.GetProperty("needsReReview").GetBoolean());
        Assert.Contains("cancelled", demotedLink.GetProperty("staleReason").GetString());
    }

    [Fact]
    public async Task Editing_supporting_fact_invalidates_and_demotes_confirmed_trace_link()
    {
        var client = factory.CreateClient();

        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Fact Edit Demotion Test",
            reference = "DEMOTE-02",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-DEMO-02",
            description = "Test demotion on fact edit",
            status = "Open",
            actor = "tester"
        });
        var invId = (await invResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        await UploadScopedAsync(client, invId, "batch_record.csv",
            "line,raw_lot,production_batch,qty\n1,RM-DEMO-02,PB-DEMO-02,500 kg\n");

        var genResp = await client.PostAsJsonAsync($"/api/relationship-proposals/generate?investigationId={invId}", new { });
        var genResult = await genResp.Content.ReadFromJsonAsync<JsonElement>();
        var proposal = genResult.GetProperty("proposals").EnumerateArray().First(p => p.GetProperty("fromId").GetString() == "RM-DEMO-02");
        var propId = proposal.GetProperty("id").GetString()!;

        // Confirm link
        var confResp = await client.PostAsJsonAsync($"/api/relationship-proposals/{propId}/confirm", new { reviewer = "lead", notes = "initially accepted" });
        Assert.True(confResp.IsSuccessStatusCode);

        // Find supporting fact
        var facts = await client.GetFromJsonAsync<JsonElement>($"/api/facts?investigationId={invId}");
        var fact = facts.EnumerateArray().First(f => f.GetProperty("rawValue").GetString()!.Contains("RM-DEMO-02"));
        var factId = fact.GetProperty("id").GetString()!;

        // Edit the supporting fact
        var patchResp = await client.PatchAsJsonAsync($"/api/facts/{factId}", new { claimObject = "PB-DEMO-02 (retyped)", reason = "quantity retyped from clearer scan", reviewer = "auditor" });
        Assert.True(patchResp.IsSuccessStatusCode);

        // Verify the trace link is demoted to Candidate and marked NeedsReReview
        var links = await client.GetFromJsonAsync<JsonElement>($"/api/relationship-proposals?investigationId={invId}");
        var demotedLink = links.EnumerateArray().First(l => l.GetProperty("id").GetString() == propId);
        Assert.Equal("Candidate", demotedLink.GetProperty("state").GetString());
        Assert.True(demotedLink.GetProperty("needsReReview").GetBoolean());
        Assert.Contains("retyped", demotedLink.GetProperty("staleReason").GetString());
    }

    [Fact]
    public async Task Ungrounded_facts_without_source_locations_do_not_generate_proposals()
    {
        var client = factory.CreateClient();

        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Ungrounded Claim Case",
            reference = "UNGROUNDED-01",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-UG-01",
            description = "Claims without a verified source location must not produce proposals",
            status = "Open",
            actor = "tester"
        });
        var invId = (await invResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var upload = await UploadScopedAsync(client, invId, "supplier_delivery.csv",
            "ref,supplier,lot,qty,unit\nDEL-UG,Acme Spice Imports,RM-UG-01,50,kg\n");
        var docId = Guid.Parse((await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("documentId").GetString()!);

        // Inject a candidate claim that failed quote grounding (no verified source location).
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecallGraphDbContext>();
            var wsId = db.Workspaces.Select(w => w.Id).First();
            db.ExtractedFacts.Add(new ExtractedFact
            {
                WorkspaceId = wsId,
                SourceDocumentId = docId,
                SourceLocationId = null,
                FactType = FactType.SupplierDelivery,
                ClaimType = "llm extracted",
                ClaimSubject = "RM-UG-02",
                ClaimObject = "PB-UG-02",
                RawValue = "RM-UG-02 consumed by production batch PB-UG-02, 50 kg",
                NormalizedValue = "RM-UG-02",
                EvidenceText = "RM-UG-02 consumed by production batch PB-UG-02, 50 kg",
                ReviewStatus = ReviewStatus.Unreviewed,
                ExtractionMethod = "ai-extraction",
                ValidationOutcome = "needs_review_unverified_source",
                ConfidenceNote = "test: ungrounded candidate claim"
            });
            db.SaveChanges();
        }

        var genResp = await client.PostAsJsonAsync($"/api/relationship-proposals/generate?investigationId={invId}", new { });
        Assert.True(genResp.IsSuccessStatusCode);
        var genResult = await genResp.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, genResult.GetProperty("skippedUngroundedCount").GetInt32());
        Assert.True(genResult.GetProperty("generatedCount").GetInt32() >= 1, "Grounded facts must still generate proposals");

        var proposals = genResult.GetProperty("proposals").EnumerateArray().ToList();
        Assert.DoesNotContain(proposals, p => p.GetProperty("fromId").GetString() == "RM-UG-02");
        Assert.DoesNotContain(proposals, p => p.GetProperty("toId").GetString() == "PB-UG-02");
    }

    [Fact]
    public async Task Dual_investigation_gap_isolation_prevents_cross_case_resolution()
    {
        var client = factory.CreateClient();

        var inv1Resp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Investigation Alpha",
            reference = "INV-ALPHA",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-ALPHA",
            description = "Alpha case",
            status = "Open",
            actor = "tester"
        });
        var inv1Id = (await inv1Resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var inv2Resp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Investigation Beta",
            reference = "INV-BETA",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-BETA",
            description = "Beta case",
            status = "Open",
            actor = "tester"
        });
        var inv2Id = (await inv2Resp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        // Upload evidence to Alpha that causes a quantity mismatch
        await UploadScopedAsync(client, inv1Id, "alpha_delivery.csv", "ref,supplier,lot,qty,unit\nDEL-A,Acme Imports,RM-ALPHA,2000,kg\n");
        await UploadScopedAsync(client, inv1Id, "alpha_batch.csv", "ref,raw_lot,production_batch,qty,unit\nWO-A,RM-ALPHA,PB-ALPHA,1000,kg\n");
        await client.PostAsJsonAsync($"/api/relationship-proposals/generate?investigationId={inv1Id}", new { });

        // Refresh Alpha findings
        var alphaFindings = await client.GetFromJsonAsync<JsonElement>($"/api/gaps?investigationId={inv1Id}&refresh=true");
        Assert.Contains(alphaFindings.EnumerateArray(), f => f.GetProperty("kind").GetString() == "quantity-mismatch" && f.GetProperty("status").GetString() == "Open");

        // Refresh Beta findings (empty evidence)
        var betaFindings = await client.GetFromJsonAsync<JsonElement>($"/api/gaps?investigationId={inv2Id}&refresh=true");
        Assert.DoesNotContain(betaFindings.EnumerateArray(), f => f.GetProperty("kind").GetString() == "quantity-mismatch");

        // Check Alpha findings again: MUST NOT be marked Resolved by Beta's refresh!
        var alphaCheck = await client.GetFromJsonAsync<JsonElement>($"/api/gaps?investigationId={inv1Id}");
        var openAlphaMismatch = alphaCheck.EnumerateArray().FirstOrDefault(f => f.GetProperty("kind").GetString() == "quantity-mismatch");
        Assert.NotNull(openAlphaMismatch.GetProperty("status").GetString());
        Assert.Equal("Open", openAlphaMismatch.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Dynamic_suspect_lot_detection_identifies_non_0427_lots()
    {
        var client = factory.CreateClient();

        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Lead In Cinnamon",
            reference = "FDA-CORE-93340",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-093340",
            description = "Lead chromate suspect",
            status = "Open",
            actor = "tester"
        });
        Assert.True(invResp.IsSuccessStatusCode);

        // Upload a document declaring RM-093340
        await UploadAsync(client, "lab_finding.csv", "sample_id,lot,result\nS-01,RM-093340,positive lead\n");

        var lots = await client.GetFromJsonAsync<JsonElement>("/api/workspace/lots");
        var suspectLot = lots.EnumerateArray().FirstOrDefault(l => l.GetProperty("code").GetString() == "RM-093340");
        Assert.NotNull(suspectLot.GetProperty("code").GetString());
        Assert.True(suspectLot.GetProperty("isSuspect").GetBoolean());

        // A raw lot that merely contains "0427" must not be flagged unless investigation metadata names it
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecallGraphDbContext>();
            var wsId = db.Workspaces.Select(w => w.Id).First();
            db.RawMaterialLots.Add(new RawMaterialLot { WorkspaceId = wsId, Code = "RM-0427B", ProductName = "Decoy lot", Status = "active", Notes = "negative-case decoy" });
            db.SaveChanges();
            try
            {
                var lotsWithDecoy = await client.GetFromJsonAsync<JsonElement>("/api/workspace/lots");
                var decoy = lotsWithDecoy.EnumerateArray().FirstOrDefault(l => l.GetProperty("code").GetString() == "RM-0427B");
                Assert.NotNull(decoy.GetProperty("code").GetString());
                Assert.False(decoy.GetProperty("isSuspect").GetBoolean());
            }
            finally
            {
                var decoyRow = db.RawMaterialLots.First(x => x.WorkspaceId == wsId && x.Code == "RM-0427B");
                db.RawMaterialLots.Remove(decoyRow);
                db.SaveChanges();
            }
        }
    }

    [Fact]
    public async Task Export_formats_preserve_scope_certainty_and_sanitize_csv_formulas()
    {
        var client = factory.CreateClient();

        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Export Test Case",
            reference = "EXP-01",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-EXP-01",
            description = "Formula injection & scope certainty test",
            status = "Open",
            actor = "tester"
        });
        var invId = (await invResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        // Upload delivery with formula trigger in supplier column
        await UploadScopedAsync(client, invId, "delivery.csv", "ref,supplier,lot,qty,unit\nDEL-01,=1+1,RM-EXP-01,100,kg\n");
        await UploadScopedAsync(client, invId, "batch.csv", "ref,raw_lot,production_batch,qty,unit\nB-1,RM-EXP-01,PB-EXP-01,100,kg\n");
        await UploadScopedAsync(client, invId, "finished.csv", "ref,production_batch,finished_lot,qty,unit\nF-1,PB-EXP-01,FL-EXP-01,100,kg\n");
        await client.PostAsJsonAsync($"/api/relationship-proposals/generate?investigationId={invId}", new { });

        // Run trace with candidate links
        var traceResp = await client.PostAsJsonAsync("/api/trace-runs", new
        {
            investigationId = Guid.Parse(invId),
            selectedType = "raw-lot",
            selectedId = "RM-EXP-01",
            direction = "both",
            reviewer = "operator"
        });
        var trace = await traceResp.Content.ReadFromJsonAsync<JsonElement>();
        var runId = trace.GetProperty("runId").GetString()!;

        // Test CSV export
        var csvResp = await client.GetAsync($"/api/trace-runs/{runId}/export?format=csv");
        Assert.True(csvResp.IsSuccessStatusCode);
        var csvContent = await csvResp.Content.ReadAsStringAsync();

        // Assert formula triggers are escaped with single quote in CSV cells
        Assert.Contains("POSSIBLE_FINISHED_LOT", csvContent);
        Assert.DoesNotContain("\"=1+1\"", csvContent);
        Assert.Contains("\"'=1+1\"", csvContent);

        // Verify CsvCell formula trigger escaping across all trigger characters
        Assert.Equal("\"'=cmd|' /C calc'!A0\"", ExportFormatter.CsvCell("=cmd|' /C calc'!A0"));
        Assert.Equal("\"'+calc\"", ExportFormatter.CsvCell("+calc"));
        Assert.Equal("\"'-calc\"", ExportFormatter.CsvCell("-calc"));
        Assert.Equal("\"'@calc\"", ExportFormatter.CsvCell("@calc"));

        // Test HTML export
        var htmlResp = await client.GetAsync($"/api/trace-runs/{runId}/export?format=html");
        Assert.True(htmlResp.IsSuccessStatusCode);
        var htmlContent = await htmlResp.Content.ReadAsStringAsync();
        Assert.Contains("Confirmed Affected Scope", htmlContent);
        Assert.Contains("Possible Exposure Scope", htmlContent);
        Assert.Contains("FL-EXP-01", htmlContent);

        // Test XLSX export: real OpenXML workbook, structured sheets, formula-safe cells.
        var xlsxResp = await client.GetAsync($"/api/trace-runs/{runId}/export?format=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsxResp.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", xlsxResp.Content.Headers.ContentType?.MediaType);
        Assert.Contains("Klarrow_EXP-01_", xlsxResp.Content.Headers.ContentDisposition?.FileNameStar ?? "");
        var xlsxBytes = await xlsxResp.Content.ReadAsByteArrayAsync();
        Assert.True(xlsxBytes.Length > 0);
        Assert.Equal(new byte[] { 0x50, 0x4B, 0x03, 0x04 }, xlsxBytes[..4]);

        using (var stream = new MemoryStream(xlsxBytes))
        using (var workbook = new XLWorkbook(stream))
        {
            Assert.Equal(5, workbook.Worksheets.Count);
            Assert.NotNull(workbook.Worksheet("Investigation Summary"));
            var evidence = workbook.Worksheet("Evidence & Facts");
            var traceSheet = workbook.Worksheet("Trace Links");
            var findingsSheet = workbook.Worksheet("Findings & Gaps");
            var auditSheet = workbook.Worksheet("Audit Trail");

            Assert.Equal("tblEvidence", evidence.Table("tblEvidence").Name);
            Assert.Equal("tblTraceLinks", traceSheet.Table("tblTraceLinks").Name);
            Assert.Equal("tblFindingsGaps", findingsSheet.Table("tblFindingsGaps").Name);
            Assert.Equal("tblAudit", auditSheet.Table("tblAudit").Name);

            Assert.Equal("Source Document", evidence.Cell(3, 1).GetString());
            Assert.Equal("Supporting Excerpt", evidence.Cell(3, 19).GetString());
            Assert.Equal("From ID", traceSheet.Cell(3, 2).GetString());
            Assert.Equal("Recommendation", findingsSheet.Cell(3, 11).GetString());
            Assert.Equal("Payload JSON", auditSheet.Cell(3, 7).GetString());

            Assert.Equal(3, evidence.SheetView.SplitRow);
            Assert.Equal(3, traceSheet.SheetView.SplitRow);
            Assert.Equal(2, evidence.PageSetup.PagesWide);
            Assert.Equal(2, traceSheet.PageSetup.PagesWide);
            Assert.Equal(XLPageOrientation.Landscape, evidence.PageSetup.PageOrientation);
            Assert.Equal(XLPageOrientation.Landscape, traceSheet.PageSetup.PageOrientation);
            Assert.True(evidence.PageSetup.PrintAreas.Any());
            Assert.True(evidence.Column(1).Width > 5);
            Assert.True(evidence.Cell(4, 1).Style.Alignment.WrapText);

            // The synthetic supplier "=1+1" is untrusted text, not a formula.
            var fromIdCell = traceSheet.RowsUsed()
                .Select(r => r.Cell(2))
                .FirstOrDefault(c => c.GetString() == "=1+1");
            Assert.NotNull(fromIdCell);
            Assert.False(fromIdCell!.HasFormula);
            Assert.Equal(XLDataType.Text, fromIdCell.DataType);
            Assert.True(fromIdCell.Style.IncludeQuotePrefix);

            // Quantity stays numeric and uses an invariant thousands/decimal format.
            var quantityCell = evidence.RowsUsed()
                .Select(r => r.Cell(8))
                .First(c => c.DataType == XLDataType.Number && c.GetDouble() == 100d);
            Assert.Contains("#,##0", quantityCell.Style.NumberFormat.Format);
        }
    }
}

public sealed class PilotFactory : WebApplicationFactory<Program>
{
    public string DataRoot { get; } = Path.Combine(Path.GetTempPath(), "recallgraph-pilot-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Storage:DataRoot", DataRoot);
        builder.UseSetting("Storage:UploadsPath", Path.Combine(DataRoot, "uploads"));
        builder.UseSetting("Seed:DemoEnabled", "false");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5 && Directory.Exists(DataRoot); attempt++)
        {
            try { Directory.Delete(DataRoot, true); }
            catch (IOException) { Thread.Sleep(200); }
        }
    }
}

public sealed class PilotWorkspaceTests(PilotFactory factory) : IClassFixture<PilotFactory>
{
    [Fact]
    public async Task Fresh_pilot_installation_has_no_seeded_demo_documents()
    {
        var client = factory.CreateClient();
        var home = await client.GetFromJsonAsync<JsonElement>("/api/workspace/home");
        Assert.False(home.GetProperty("isDemo").GetBoolean());
        Assert.Equal("pilot", home.GetProperty("mode").GetString());
        Assert.False(home.GetProperty("hasSyntheticData").GetBoolean());
        Assert.Equal(0, home.GetProperty("syntheticDocumentCount").GetInt32());
        Assert.Equal(0, home.GetProperty("documentCount").GetInt32());
        Assert.Equal(0, home.GetProperty("factCount").GetInt32());

        var investigations = await client.GetFromJsonAsync<JsonElement>("/api/investigations");
        Assert.Equal(0, investigations.GetArrayLength());
    }
}

public sealed class PilotExportFactory : WebApplicationFactory<Program>
{
    public string DataRoot { get; } = Path.Combine(Path.GetTempPath(), "recallgraph-pilot-export-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Storage:DataRoot", DataRoot);
        builder.UseSetting("Storage:UploadsPath", Path.Combine(DataRoot, "uploads"));
        builder.UseSetting("Seed:DemoEnabled", "false");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5 && Directory.Exists(DataRoot); attempt++)
        {
            try { Directory.Delete(DataRoot, true); }
            catch (IOException) { Thread.Sleep(200); }
        }
    }
}

public sealed class PilotExportTests(PilotExportFactory factory) : IClassFixture<PilotExportFactory>
{
    [Fact]
    public async Task Pilot_workspace_exports_use_pilot_provenance_notice()
    {
        var client = factory.CreateClient();

        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Pilot Export Case",
            reference = "PILOT-EXP-01",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-PILOT-01",
            description = "Pilot export provenance test",
            status = "Open",
            actor = "tester"
        });
        var invId = (await invResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        await PilotUploadScopedAsync(client, invId, "pilot_delivery.csv", "ref,supplier,lot,qty,unit\nPIL-DEL,Acme Imports,RM-PILOT-01,100,kg\n");
        await PilotUploadScopedAsync(client, invId, "pilot_batch.csv", "ref,raw_lot,production_batch,qty,unit\nPIL-B,RM-PILOT-01,PB-PILOT-01,100,kg\n");
        await PilotUploadScopedAsync(client, invId, "pilot_finished.csv", "ref,production_batch,finished_lot,qty,unit\nPIL-F,PB-PILOT-01,FL-PILOT-01,100,kg\n");
        await client.PostAsJsonAsync($"/api/relationship-proposals/generate?investigationId={invId}", new { });

        var traceResp = await client.PostAsJsonAsync("/api/trace-runs", new
        {
            investigationId = Guid.Parse(invId),
            selectedType = "raw-lot",
            selectedId = "RM-PILOT-01",
            direction = "both",
            reviewer = "operator"
        });
        Assert.True(traceResp.IsSuccessStatusCode);
        var runId = (await traceResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetString()!;

        var csv = await (await client.GetAsync($"/api/trace-runs/{runId}/export?format=csv")).Content.ReadAsStringAsync();
        Assert.Contains("PILOT WORKSPACE", csv);
        Assert.Contains("NOT CERTIFIED FOR REGULATORY CLEARANCE", csv);
        Assert.DoesNotContain("SYNTHETIC DEMONSTRATION DATA", csv);
        Assert.Contains("Mode=pilot", csv);
        Assert.Contains("SyntheticEvidence=false", csv);

        var html = await (await client.GetAsync($"/api/trace-runs/{runId}/export?format=html")).Content.ReadAsStringAsync();
        Assert.Contains("PILOT WORKSPACE", html);
        Assert.DoesNotContain("SYNTHETIC DEMONSTRATION DATA", html);
        Assert.Contains("Workspace mode:</strong> pilot", html);
        Assert.Contains("Synthetic evidence:</strong> No", html);

        var json = await client.GetFromJsonAsync<JsonElement>($"/api/trace-runs/{runId}/export?format=json");
        Assert.Equal("pilot", json.GetProperty("workspaceMode").GetString());
        Assert.False(json.GetProperty("containsSyntheticEvidence").GetBoolean());
        Assert.Contains("PILOT WORKSPACE", json.GetProperty("disclaimer").GetString());
        Assert.DoesNotContain("SYNTHETIC DEMONSTRATION DATA", json.GetProperty("disclaimer").GetString());
    }

    private static async Task PilotUploadScopedAsync(HttpClient client, string investigationId, string fileName, string contents)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(investigationId), "investigationId");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(contents));
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "file", FileName = fileName };
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file);
        Assert.True((await client.PostAsync("/api/documents/upload", form)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task Seeding_synthetic_fixture_into_pilot_workspace_keeps_pilot_mode_and_labels_synthetic_evidence()
    {
        var client = factory.CreateClient();
        var seed = await client.PostAsJsonAsync("/api/admin/seed", new { reset = false });
        Assert.True(seed.IsSuccessStatusCode);

        var home = await client.GetFromJsonAsync<JsonElement>("/api/workspace/home");
        Assert.Equal("pilot", home.GetProperty("mode").GetString());
        Assert.False(home.GetProperty("isDemo").GetBoolean());
        Assert.True(home.GetProperty("hasSyntheticData").GetBoolean());
        Assert.True(home.GetProperty("syntheticDocumentCount").GetInt32() >= 24);
        Assert.False(string.IsNullOrEmpty(home.GetProperty("primaryInvestigationId").GetString()));

        var traceResp = await client.PostAsJsonAsync("/api/trace-runs", new { selectedType = "raw-lot", selectedId = "RM-0427", direction = "both" });
        Assert.True(traceResp.IsSuccessStatusCode);
        var runId = (await traceResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("runId").GetString()!;

        var csv = await (await client.GetAsync($"/api/trace-runs/{runId}/export?format=csv")).Content.ReadAsStringAsync();
        Assert.Contains("PILOT WORKSPACE", csv);
        Assert.Contains("seeded synthetic demonstration link(s)", csv);
        Assert.Contains("Mode=pilot", csv);
        Assert.Contains("SyntheticEvidence=true", csv);

        var html = await (await client.GetAsync($"/api/trace-runs/{runId}/export?format=html")).Content.ReadAsStringAsync();
        Assert.Contains("PILOT WORKSPACE", html);
        Assert.Contains("Synthetic evidence:</strong> Yes", html);

        var json = await client.GetFromJsonAsync<JsonElement>($"/api/trace-runs/{runId}/export?format=json");
        Assert.Equal("pilot", json.GetProperty("workspaceMode").GetString());
        Assert.True(json.GetProperty("containsSyntheticEvidence").GetBoolean());
    }
}

public sealed class ApiKeyFactory : WebApplicationFactory<Program>
{
    public string DataRoot { get; } = Path.Combine(Path.GetTempPath(), "recallgraph-apikey-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Storage:DataRoot", DataRoot);
        builder.UseSetting("Storage:UploadsPath", Path.Combine(DataRoot, "uploads"));
        builder.UseSetting("Seed:DemoEnabled", "false");
        builder.UseSetting("RecallGraph:ApiKey", "test-key");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5 && Directory.Exists(DataRoot); attempt++)
        {
            try { Directory.Delete(DataRoot, true); }
            catch (IOException) { Thread.Sleep(200); }
        }
    }
}

public sealed class ApiKeyTests(ApiKeyFactory factory) : IClassFixture<ApiKeyFactory>
{
    [Fact]
    public async Task Configured_api_key_protects_business_routes_but_not_health()
    {
        var client = factory.CreateClient();
        var health = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
        var healthBody = await health.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("token", healthBody.GetProperty("accessControl").GetProperty("mode").GetString());
        Assert.Contains("Not enterprise identity/access management", healthBody.GetProperty("accessControl").GetProperty("notice").GetString());

        var unauthorized = await client.GetAsync("/api/workspace/home");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);

        var contentWithoutKey = await client.GetAsync($"/api/documents/{Guid.NewGuid()}/content?locator={Uri.EscapeDataString("lines 1-1")}");
        Assert.Equal(HttpStatusCode.Unauthorized, contentWithoutKey.StatusCode);

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/workspace/home");
        request.Headers.Add("X-Api-Key", "test-key");
        var authorized = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, authorized.StatusCode);
    }
}

public sealed class StubAiProvider(List<LlmFactCandidate>? candidates = null) : IAiExtractionProvider
{
    private static readonly List<LlmFactCandidate> DefaultCandidates =
    [
        new(
            RawValue: "DSP-9001", NormalizedValue: null, DocumentType: null, IssuingEntity: null, DocumentReference: null,
            FactType: "dispatch", ClaimType: "llm extracted", ClaimSubject: "Customer Stub", ClaimObject: null,
            Quantity: 500m, QuantityUnit: "units", EventDate: null,
            EvidenceText: "DSP-9001 | FL-1000 | 500 units | Customer Stub", SourceLocation: null,
            ConfidenceNote: "stub", Confidence: 0.5, IsRelation: false, RelationFrom: null, RelationTo: null, Notes: null),
        new(
            RawValue: "FL-1000", NormalizedValue: null, DocumentType: null, IssuingEntity: null, DocumentReference: null,
            FactType: "finished_lot", ClaimType: "llm extracted", ClaimSubject: "Customer Stub", ClaimObject: null,
            Quantity: null, QuantityUnit: null, EventDate: null,
            EvidenceText: "this quoted text is not present anywhere", SourceLocation: null,
            ConfidenceNote: "stub", Confidence: 0.5, IsRelation: false, RelationFrom: null, RelationTo: null, Notes: null)
    ];

    public AiProviderStatusDto Status => new()
    {
        IsConfigured = true,
        Mode = "configured",
        Provider = "Stub Llama",
        ModelId = "stub-model-1",
        Endpoint = "stub://local"
    };

    public Task<LlmExtractionResult> ExtractAsync(string documentText, string? imageBase64, string? imageMimeType, string fileName, CancellationToken ct = default)
        => Task.FromResult(new LlmExtractionResult(
            candidates ?? DefaultCandidates,
            true, null, "{}", "Stub Llama", "stub-model-1"));
}

public sealed class DeterministicOnlyStubProvider : IAiExtractionProvider
{
    public bool ExtractCalled { get; private set; }
    public AiProviderStatusDto Status => new()
    {
        IsConfigured = false,
        Mode = "deterministic_only",
        Provider = "none",
        ModelId = string.Empty,
        Message = "Deterministic-only evaluation mode."
    };
    public Task<LlmExtractionResult> ExtractAsync(string documentText, string? imageBase64, string? imageMimeType, string fileName, CancellationToken ct = default)
    {
        ExtractCalled = true;
        return Task.FromResult(new LlmExtractionResult([], false, "disabled", string.Empty, "none", string.Empty));
    }
}

public sealed class GroundingDedupFactory(IAiExtractionProvider? provider = null) : WebApplicationFactory<Program>
{
    public string DataRoot { get; } = Path.Combine(Path.GetTempPath(), "recallgraph-grounding-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("Storage:DataRoot", DataRoot);
        builder.UseSetting("Storage:UploadsPath", Path.Combine(DataRoot, "uploads"));
        builder.UseSetting("Seed:DemoEnabled", "false");
        if (provider != null)
            builder.ConfigureServices(services => services.AddSingleton(provider));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var attempt = 0; attempt < 5 && Directory.Exists(DataRoot); attempt++)
        {
            try { Directory.Delete(DataRoot, true); }
            catch (IOException) { Thread.Sleep(200); }
        }
    }
}

public sealed class GroundingAndDedupTests
{
    [Fact]
    public async Task Extraction_marks_quote_verified_only_when_quote_is_verbatim_at_assigned_line()
    {
        using var local = new GroundingDedupFactory(new StubAiProvider());
        var client = local.CreateClient();

        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Grounding case",
            reference = "GRND-1",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-9001",
            description = "grounding regression",
            status = "Open",
            actor = "tester"
        });
        Assert.True(invResp.IsSuccessStatusCode);
        var invId = (await invResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(invId), "investigationId");
        var docText = string.Join("\n",
            "QUALITY ALERT",
            "To: buyer@example",
            "Subject: grounding regression",
            "",
            "DSP-9001 | FL-1000 | 500 units | Customer Stub",
            "",
            "Filler line seven",
            "Our laboratory analysis of retained samples from production batch CS-CIN-9999 returned a lead result above the internal specification limit.",
            "",
            "Filler line ten",
            "",
            "Filler line twelve",
            "",
            "Filler line fourteen",
            "",
            "Filler line sixteen",
            "",
            "Kandy Highland Spice Mill (Pvt) Ltd",
            "");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(docText));
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "file", FileName = "dispatch_stub.txt" };
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file);
        var upload = await client.PostAsync("/api/documents/upload", form);
        Assert.True(upload.IsSuccessStatusCode);
        var docId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("documentId").GetGuid();

        using var scope = local.Services.CreateScope();
        var ws = await scope.ServiceProvider.GetRequiredService<WorkspaceService>().GetOrCreateDemoAsync();
        var run = await scope.ServiceProvider.GetRequiredService<ExtractionService>().RunAsync(ws.Id, new List<Guid> { docId }, "stub-test");
        Assert.Equal("completed", run.Status);

        var db = scope.ServiceProvider.GetRequiredService<RecallGraphDbContext>();
        var facts = await db.ExtractedFacts.Where(x => x.SourceDocumentId == docId).ToListAsync();
        var locations = await db.SourceLocations.Where(x => x.SourceDocumentId == docId).ToListAsync();

        var verbatim = facts.Single(x => x.RawValue == "DSP-9001");
        Assert.Equal("quote_verified", verbatim.ValidationOutcome);
        Assert.Equal(5, locations.Single(x => x.Id == verbatim.SourceLocationId).LineStart);
        Assert.Equal("exact_quote", verbatim.SourceMatchMethod);

        var unlocated = facts.Single(x => x.RawValue == "FL-1000");
        Assert.Equal("identifier_matched_quote_unverified", unlocated.ValidationOutcome);
        Assert.Equal(5, locations.Single(x => x.Id == unlocated.SourceLocationId).LineStart);
    }

    [Fact]
    public async Task Grounding_distinguishes_value_in_quote_from_adjacent_identifier_and_missing_sources()
    {
        static LlmFactCandidate Candidate(string raw, string evidence) => new(
            RawValue: raw, NormalizedValue: null, DocumentType: null, IssuingEntity: null, DocumentReference: null,
            FactType: "note", ClaimType: "llm extracted", ClaimSubject: raw, ClaimObject: null,
            Quantity: null, QuantityUnit: null, EventDate: null,
            EvidenceText: evidence, SourceLocation: null,
            ConfidenceNote: "grounding regression", Confidence: 0.5, IsRelation: false,
            RelationFrom: null, RelationTo: null, Notes: null);

        var provider = new StubAiProvider(
        [
            Candidate("320 kg", "Received 320 kg of ground cinnamon."),
            Candidate("RM-0873", "Batch PB-2026-1012-A used the material."),
            Candidate("FL-1021A", "Unrelated quote about supplier approval."),
            Candidate("RM-9999", "This quote does not exist anywhere."),
            Candidate("RM-0000", "No such quote or value."),
            Candidate("180 kg", "Issued  180 kg to production."),
            Candidate("RM-1234", "Reference RM-I234 appears here."),
            Candidate("0", "Received 0.5 kg sample.")
        ]);

        using var local = new GroundingDedupFactory(provider);
        var client = local.CreateClient();
        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Grounding semantics case",
            reference = "GRND-2",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-0873",
            description = "grounding semantics regression",
            status = "Open",
            actor = "tester"
        });
        Assert.True(invResp.IsSuccessStatusCode);
        var invId = (await invResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        var docText = string.Join("\n",
            "Supplier: Acme",
            "Received 320 kg of ground cinnamon.",
            "Batch PB-2026-1012-A used the material.",
            "Reference RM-0873 appears here.",
            "The next production line contains a finished lot.",
            "FL-1021A was packed.",
            "Another line mentions RM-9999.",
            "Issued  180 kg to production.",
            "Reference RM-I234 appears here.",
            "Received 0.5 kg sample.",
            "");
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(invId), "investigationId");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(docText));
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "file", FileName = "grounding_semantics.txt" };
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file);
        var upload = await client.PostAsync("/api/documents/upload", form);
        Assert.True(upload.IsSuccessStatusCode);
        var docId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("documentId").GetGuid();

        using var scope = local.Services.CreateScope();
        var ws = await scope.ServiceProvider.GetRequiredService<WorkspaceService>().GetOrCreateDemoAsync();
        var run = await scope.ServiceProvider.GetRequiredService<ExtractionService>().RunAsync(ws.Id, new List<Guid> { docId }, "stub-test");
        Assert.Equal("completed", run.Status);

        var db = scope.ServiceProvider.GetRequiredService<RecallGraphDbContext>();
        var facts = await db.ExtractedFacts.Where(x => x.SourceDocumentId == docId).ToListAsync();
        var locations = await db.SourceLocations.Where(x => x.SourceDocumentId == docId).ToListAsync();

        var inQuote = facts.Single(x => x.RawValue == "320 kg");
        Assert.Equal("quote_verified", inQuote.ValidationOutcome);
        Assert.Equal("exact_quote", inQuote.SourceMatchMethod);
        var inQuoteLoc = locations.Single(x => x.Id == inQuote.SourceLocationId);
        Assert.Equal(2, inQuoteLoc.LineStart);

        var adjacent = facts.Single(x => x.RawValue == "RM-0873");
        Assert.Equal("context_match_value_adjacent", adjacent.ValidationOutcome);
        Assert.Equal("adjacent_line_context", adjacent.SourceMatchMethod);
        var adjacentLoc = locations.Single(x => x.Id == adjacent.SourceLocationId);
        Assert.Equal(3, adjacentLoc.LineStart);
        Assert.Equal(4, adjacentLoc.LineEnd);
        Assert.DoesNotContain("RM-0873", adjacent.SourceQuote, StringComparison.OrdinalIgnoreCase);

        var identifierElsewhere = facts.Single(x => x.RawValue == "FL-1021A");
        Assert.Equal("identifier_matched_quote_unverified", identifierElsewhere.ValidationOutcome);
        Assert.Equal("identifier_in_line", identifierElsewhere.SourceMatchMethod);
        Assert.Equal(6, locations.Single(x => x.Id == identifierElsewhere.SourceLocationId).LineStart);

        var quoteMissing = facts.Single(x => x.RawValue == "RM-9999");
        Assert.Equal("identifier_matched_quote_unverified", quoteMissing.ValidationOutcome);
        Assert.Equal(7, locations.Single(x => x.Id == quoteMissing.SourceLocationId).LineStart);

        var missing = facts.Single(x => x.RawValue == "RM-0000");
        Assert.Equal("needs_review_unverified_source", missing.ValidationOutcome);
        Assert.Null(missing.SourceLocationId);
        Assert.Equal("unmatched", missing.SourceMatchMethod);

        var whitespaceNormalized = facts.Single(x => x.RawValue == "180 kg");
        Assert.Equal("quote_verified", whitespaceNormalized.ValidationOutcome);
        Assert.Equal("180 kg", whitespaceNormalized.RawValue);

        var nearMatch = facts.Single(x => x.RawValue == "RM-1234");
        Assert.NotEqual("quote_verified", nearMatch.ValidationOutcome);
        Assert.Equal(9, locations.Single(x => x.Id == nearMatch.SourceLocationId).LineStart);

        var decimalPrefix = facts.Single(x => x.RawValue == "0");
        Assert.NotEqual("quote_verified", decimalPrefix.ValidationOutcome);
    }

    [Fact]
    public async Task Rejecting_false_positive_dispatch_removes_orphaned_auto_registered_customer_and_keeps_trace_clean()
    {
        using var local = new GroundingDedupFactory();
        var client = local.CreateClient();

        using var form = new MultipartFormDataContent();
        var contents = "Run 2 output: PB-2026-1013-B | FL-1034C | 2,800 units | awaiting dispatch allocation\n";
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(contents));
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "file", FileName = "08_dispatch_note_DSP-5012.txt" };
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file);
        var upload = await client.PostAsync("/api/documents/upload", form);
        Assert.True(upload.IsSuccessStatusCode);

        using var scope = local.Services.CreateScope();
        var ws = await scope.ServiceProvider.GetRequiredService<WorkspaceService>().GetOrCreateDemoAsync();
        var relationships = scope.ServiceProvider.GetRequiredService<RelationshipService>();
        var trace = scope.ServiceProvider.GetRequiredService<TraceService>();
        var db = scope.ServiceProvider.GetRequiredService<RecallGraphDbContext>();

        var generated = await relationships.GenerateProposalsAsync(ws.Id, null);
        var falsePositive = generated.Proposals.Single(x => x.ToType == "customer" && x.ToId.Contains("Run 2 output", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(await db.Customers.FirstOrDefaultAsync(x => x.WorkspaceId == ws.Id && x.Name == falsePositive.ToId));

        var rejected = await relationships.RejectAsync(ws.Id, falsePositive.Id, new ReviewDecisionDto { Reviewer = "reviewer", Notes = "False-positive dispatch phrase, not a customer." });
        Assert.Equal(LinkState.Rejected, rejected.State);

        Assert.Null(await db.Customers.FirstOrDefaultAsync(x => x.WorkspaceId == ws.Id && x.Name == falsePositive.ToId));
        Assert.Contains(await db.AuditEvents.Where(x => x.WorkspaceId == ws.Id).ToListAsync(), x => x.EventType == "relationship_rejected" && x.TargetId == falsePositive.Id.ToString());

        var result = await trace.RunAsync(ws.Id, new TraceRunRequestDto
        {
            SelectedType = "production-batch",
            SelectedId = "PB-2026-1013-B",
            Direction = "both",
            Reviewer = "reviewer"
        });
        Assert.DoesNotContain("FL-1034C", result.AffectedFinishedLots);
        Assert.Empty(result.AffectedCustomers);
    }

    [Fact]
    public async Task Generation_registers_entities_once_and_gap_and_match_refreshes_stay_deduplicated()
    {
        using var local = new GroundingDedupFactory();
        var client = local.CreateClient();

        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Dedup case",
            reference = "DEDUP-1",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-0902",
            description = "dedup regression",
            status = "Open",
            actor = "tester"
        });
        Assert.True(invResp.IsSuccessStatusCode);
        var invId = (await invResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        static async Task<HttpResponseMessage> UploadDoc(HttpClient client, string invId, string fileName, string contents)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(invId), "investigationId");
            var file = new ByteArrayContent(Encoding.UTF8.GetBytes(contents));
            file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "file", FileName = fileName };
            file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
            form.Add(file);
            return await client.PostAsync("/api/documents/upload", form);
        }

        var up1 = await UploadDoc(client, invId, "dedup_d1.txt", "Input consumed: PB-9902 | RM-0902 | 100 kg | blend\n");
        Assert.True(up1.IsSuccessStatusCode);
        var up2 = await UploadDoc(client, invId, "dedup_d2.txt", "Run 2 input: PB-9903 | RM-O902 | 50 kg | blend\n");
        Assert.True(up2.IsSuccessStatusCode);
        var doc2Id = (await up2.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("documentId").GetGuid();

        using (var scope = local.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecallGraphDbContext>();
            var doc2 = await db.SourceDocuments.FirstAsync(x => x.Id == doc2Id);
            db.ExtractedFacts.Add(new ExtractedFact
            {
                WorkspaceId = doc2.WorkspaceId,
                SourceDocumentId = doc2Id,
                FactType = FactType.Note,
                RawValue = "RM-O902",
                ClaimType = "llm extracted",
                ClaimSubject = "RM-O902",
                EvidenceText = "no relation tokens here",
                ExtractionMethod = "ai-extraction",
                ValidationOutcome = "needs_review_unverified_source",
                ReviewStatus = ReviewStatus.Unreviewed
            });
            db.ExtractedFacts.Add(new ExtractedFact
            {
                WorkspaceId = doc2.WorkspaceId,
                SourceDocumentId = doc2Id,
                FactType = FactType.Note,
                RawValue = "RM-0902",
                NormalizedValue = "RM-0902",
                ClaimType = "llm extracted",
                ClaimSubject = "RM-0902",
                EvidenceText = "no relation tokens here",
                ExtractionMethod = "ai-extraction",
                ValidationOutcome = "needs_review_unverified_source",
                ReviewStatus = ReviewStatus.Unreviewed
            });
            await db.SaveChangesAsync();
        }

        var gen1 = await client.PostAsJsonAsync($"/api/relationship-proposals/generate?investigationId={invId}", new { });
        var gen1Body = await gen1.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, gen1Body.GetProperty("generatedCount").GetInt32());

        var gen2 = await client.PostAsJsonAsync($"/api/relationship-proposals/generate?investigationId={invId}", new { });
        var gen2Body = await gen2.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, gen2Body.GetProperty("generatedCount").GetInt32());
        Assert.Equal(2, gen2Body.GetProperty("existingCount").GetInt32());
        Assert.Equal(2, gen2Body.GetProperty("existingLinkCount").GetInt32());

        var linksBeforeReview = await client.GetFromJsonAsync<JsonElement>($"/api/relationship-proposals?investigationId={invId}");
        var firstLink = linksBeforeReview.EnumerateArray().First();
        Assert.False(firstLink.GetProperty("supportingFactsReviewed").GetBoolean());
        Assert.True(firstLink.GetProperty("unreviewedSupportingFactCount").GetInt32() > 0);
        var supportingFactId = firstLink.GetProperty("supportingFactIds")[0].GetGuid();
        var confirmFact = await client.PostAsJsonAsync($"/api/facts/{supportingFactId}/confirm", new { reviewer = "tester", notes = "reviewed for relationship semantics test" });
        Assert.True(confirmFact.IsSuccessStatusCode);

        var linksAfterReview = await client.GetFromJsonAsync<JsonElement>($"/api/relationship-proposals?investigationId={invId}");
        var reviewedLink = linksAfterReview.EnumerateArray().First(x =>
            x.GetProperty("supportingFactIds").EnumerateArray().Any(id => id.GetGuid() == supportingFactId));
        Assert.True(reviewedLink.GetProperty("supportingFactsReviewed").GetBoolean());
        Assert.Equal(0, reviewedLink.GetProperty("unreviewedSupportingFactCount").GetInt32());

        using (var scope = local.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecallGraphDbContext>();
            Assert.Equal(1, await db.RawMaterialLots.CountAsync(x => x.Code == "RM-0902"));
            Assert.Equal(1, await db.RawMaterialLots.CountAsync(x => x.Code == "RM-O902"));
            Assert.Equal(1, await db.ProductionBatches.CountAsync(x => x.Code == "PB-9902"));
            Assert.Equal(1, await db.ProductionBatches.CountAsync(x => x.Code == "PB-9903"));
            Assert.Equal(2, await db.TraceLinks.CountAsync(x => x.InvestigationId == Guid.Parse(invId)));
        }

        await client.GetFromJsonAsync<JsonElement>("/api/match-proposals?refresh=true");
        await client.GetFromJsonAsync<JsonElement>("/api/match-proposals?refresh=true");
        using (var scope = local.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecallGraphDbContext>();
            var pair = await db.MatchProposals.Where(x =>
                (x.LeftValue == "RM-0902" && x.RightValue == "RM-O902") ||
                (x.LeftValue == "RM-O902" && x.RightValue == "RM-0902")).ToListAsync();
            Assert.Single(pair);
        }

        await client.GetFromJsonAsync<JsonElement>($"/api/gaps?investigationId={invId}&refresh=true");
        var gaps = await client.GetFromJsonAsync<JsonElement>($"/api/gaps?investigationId={invId}&refresh=true");
        var keys = gaps.EnumerateArray().Select(x => x.GetProperty("stableKey").GetString()!).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        var ambiguous = gaps.EnumerateArray().Where(x => x.GetProperty("kind").GetString() == "ambiguous-identifier").ToList();
        Assert.Single(ambiguous);
        Assert.Equal("RM-0902", ambiguous[0].GetProperty("scope").GetString());
        Assert.Contains("RM-O902", ambiguous[0].GetProperty("explanation").GetString()!);
    }
}

public sealed class InvestigationDeletionTests(RecallGraphFactory factory) : IClassFixture<RecallGraphFactory>
{
    [Fact]
    public async Task Deleting_investigation_removes_scoped_evidence_and_writes_deletion_audit()
    {
        var client = factory.CreateClient();
        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Deletion regression case",
            reference = "DEL-1",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-5001",
            description = "delete regression",
            status = "Open",
            actor = "tester"
        });
        Assert.True(invResp.IsSuccessStatusCode);
        var invId = (await invResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(invId), "investigationId");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("GRN-5511 | RM-5001 | 100 kg | 2026-10-01\n"));
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "file", FileName = "delete_regression.txt" };
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file);
        var upload = await client.PostAsync("/api/documents/upload", form);
        Assert.True(upload.IsSuccessStatusCode);
        var docId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("documentId").GetGuid();

        var docsBefore = await client.GetFromJsonAsync<JsonElement>($"/api/documents?investigationId={invId}");
        Assert.Equal(1, docsBefore.GetArrayLength());

        string storedPath;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RecallGraphDbContext>();
            var doc = await db.SourceDocuments.FirstAsync(x => x.Id == docId);
            Assert.Equal(Guid.Parse(invId), doc.InvestigationId);
            storedPath = doc.StoredPath;
        }
        Assert.True(File.Exists(storedPath));

        var del = await client.DeleteAsync($"/api/investigations/{invId}?actor=deletion-test");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        var body = await del.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("deleted").GetBoolean());
        Assert.Equal(1, body.GetProperty("documentsDeleted").GetInt32());
        Assert.True(body.GetProperty("factsDeleted").GetInt32() >= 1);
        Assert.True(body.GetProperty("filesDeleted").GetInt32() >= 1);
        Assert.True(body.GetProperty("limitations").GetArrayLength() >= 1);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/investigations/{invId}")).StatusCode);
        var docsAfter = await client.GetFromJsonAsync<JsonElement>($"/api/documents?investigationId={invId}");
        Assert.Equal(0, docsAfter.GetArrayLength());
        Assert.False(File.Exists(storedPath));

        var audits = await client.GetFromJsonAsync<JsonElement>("/api/workspace/audits");
        var deletion = audits.EnumerateArray().First(x => x.GetProperty("eventType").GetString() == "investigation_deleted");
        Assert.Contains("DEL-1", deletion.GetProperty("description").GetString()!);
        Assert.Contains("\"documents\":1", deletion.GetProperty("payloadJson").GetString()!);
    }
}

public sealed class DeterministicOnlyModeTests
{
    [Fact]
    public async Task Deterministic_only_mode_skips_provider_and_records_mode_without_ai_facts()
    {
        var provider = new DeterministicOnlyStubProvider();
        using var local = new GroundingDedupFactory(provider);
        var client = local.CreateClient();

        var invResp = await client.PostAsJsonAsync("/api/investigations", new
        {
            name = "Deterministic mode case",
            reference = "DET-1",
            suspectLotType = "raw-lot",
            suspectLotId = "RM-6001",
            description = "deterministic-only regression",
            status = "Open",
            actor = "tester"
        });
        Assert.True(invResp.IsSuccessStatusCode);
        var invId = (await invResp.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetString()!;

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(invId), "investigationId");
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("DN-6601 | Acme Supplies | RM-6001 | 250 kg | 2026-10-02\n"));
        file.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data") { Name = "file", FileName = "deterministic_mode.txt" };
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(file);
        var upload = await client.PostAsync("/api/documents/upload", form);
        Assert.True(upload.IsSuccessStatusCode);
        var docId = (await upload.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("documentId").GetGuid();

        using var scope = local.Services.CreateScope();
        var ws = await scope.ServiceProvider.GetRequiredService<WorkspaceService>().GetOrCreateDemoAsync();
        var run = await scope.ServiceProvider.GetRequiredService<ExtractionService>().RunAsync(ws.Id, new List<Guid> { docId }, "deterministic-test");

        Assert.Equal("completed-deterministic", run.Status);
        Assert.Equal("DeterministicParser", run.Provider);
        Assert.Equal("none", run.ModelId);
        Assert.Equal(0, run.FactsCreated);
        Assert.False(run.IsDemo);
        Assert.False(provider.ExtractCalled);

        var db = scope.ServiceProvider.GetRequiredService<RecallGraphDbContext>();
        Assert.False(await db.ExtractedFacts.AnyAsync(x => x.SourceDocumentId == docId && x.ExtractionMethod == "ai-extraction"));
        Assert.True(await db.ExtractedFacts.AnyAsync(x => x.SourceDocumentId == docId && x.ExtractionMethod == "deterministic-parser"));

        var audits = await db.AuditEvents.Where(x => x.WorkspaceId == ws.Id).ToListAsync();
        Assert.Contains(audits, x => x.EventType == "extraction_run" && x.PayloadJson.Contains("deterministic_only"));
    }
}

public sealed class CapturingHandler : HttpMessageHandler
{
    public System.Net.Http.Headers.AuthenticationHeaderValue? Authorization { get; private set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Authorization = request.Headers.Authorization;
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"choices\":[{\"message\":{\"content\":\"{\\\"facts\\\":[]}\"}}]}", Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}

public sealed class SlowHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
    }
}

public sealed class StubHandler(string content, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
        return Task.FromResult(response);
    }
}
