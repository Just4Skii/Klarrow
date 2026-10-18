using System.Globalization;
using System.Net;
using System.Text;
using ClosedXML.Excel;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public static class ExportFormatter
{
    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r', '%'];

    public static string CsvCell(string? value)
    {
        var text = value ?? string.Empty;
        if (text.Length > 0 && FormulaTriggers.Contains(text[0])) text = "'" + text;
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }

    public static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    /// <summary>
    /// Builds the provenance notice from the persisted workspace mode and the synthetic-evidence count of the run.
    /// Demo workspaces and pilot workspaces never claim certification, compliance or operational safety.
    /// </summary>
    public static string BuildProvenanceNotice(string? mode, int syntheticEvidenceCount)
    {
        if (string.Equals(mode, "demo", StringComparison.OrdinalIgnoreCase))
            return "SYNTHETIC DEMONSTRATION DATA / NOT FOR OPERATIONAL FOOD-SAFETY DECISIONS";
        var notice = "PILOT WORKSPACE / NOT CERTIFIED FOR REGULATORY CLEARANCE: Evidence reconciliation output reflects parsed documentation and operator review. Professional verification required.";
        if (syntheticEvidenceCount > 0)
            notice += $" This snapshot includes {syntheticEvidenceCount} seeded synthetic demonstration link(s); simulated records are identified at record level and must not be treated as real evidence.";
        return notice;
    }

    private static string ResolveDisclaimer(TraceResultDto result)
    {
        if (!string.IsNullOrWhiteSpace(result.Disclaimer)) return result.Disclaimer;
        if (!string.IsNullOrWhiteSpace(result.DemoDisclaimer)) return result.DemoDisclaimer;
        var syntheticCount = result.Edges.Count(e => string.Equals(e.Origin, "seeded-demo", StringComparison.OrdinalIgnoreCase));
        return BuildProvenanceNotice(result.WorkspaceMode, syntheticCount);
    }

    public static string BuildCsv(TraceResultDto result, IReadOnlyList<AuditEventDto>? audits = null)
    {
        var disclaimer = ResolveDisclaimer(result);
        var sb = new StringBuilder();
        sb.AppendLine(CsvCell($"# Klarrow export RunId={result.RunId} InvestigationId={result.InvestigationId?.ToString() ?? "not-linked"} Selected={result.SelectedType}:{result.SelectedId} Completed={result.CompletedAt:O} Mode={result.WorkspaceMode} SyntheticEvidence={result.ContainsSyntheticEvidence.ToString().ToLowerInvariant()} Disclaimer={disclaimer}"));
        sb.AppendLine(string.Join(",", new[]
        {
            "RunId", "InvestigationId", "SelectedType", "SelectedId", "Direction", "RecordType", "EntityKind", "EntityId",
            "RelatedType", "RelatedId", "LinkKindOrCategory", "StateOrSeverity", "Quantity", "Unit", "EventDate", "EvidenceOrDetails", "SourceReferencesOrCaveat"
        }.Select(CsvCell)));

        // 1. Confirmed & Candidate trace edges
        foreach (var e in result.Edges)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                result.RunId.ToString(), result.InvestigationId?.ToString() ?? "", result.SelectedType, result.SelectedId,
                result.Direction, "EDGE", e.FromType, e.FromId, e.ToType, e.ToId, e.LinkKind, e.State.ToString(),
                e.Quantity?.ToString(CultureInfo.InvariantCulture) ?? "", e.QuantityUnit, e.EventDate?.ToString("O") ?? "", e.EvidenceText, e.SourceReferences
            }.Select(CsvCell)));
        }

        // 2. Confirmed scope finished lots
        foreach (var lot in result.AffectedFinishedLots)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                result.RunId.ToString(), result.InvestigationId?.ToString() ?? "", result.SelectedType, result.SelectedId,
                result.Direction, "CONFIRMED_FINISHED_LOT", "finished-lot", lot, "", "", "ConfirmedScope", "Confirmed", "", "", "", "Reached through confirmed evidence links", ""
            }.Select(CsvCell)));
        }

        // 3. Possible scope finished lots
        foreach (var lot in result.PossibleFinishedLots)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                result.RunId.ToString(), result.InvestigationId?.ToString() ?? "", result.SelectedType, result.SelectedId,
                result.Direction, "POSSIBLE_FINISHED_LOT", "finished-lot", lot, "", "", "PossibleScope", "Candidate", "", "", "", "Requires operator review before confirming exposure", ""
            }.Select(CsvCell)));
        }

        // 4. Confirmed scope customers
        foreach (var cust in result.AffectedCustomers)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                result.RunId.ToString(), result.InvestigationId?.ToString() ?? "", result.SelectedType, result.SelectedId,
                result.Direction, "CONFIRMED_CUSTOMER", "customer", cust, "", "", "ConfirmedDestination", "Confirmed", "", "", "", "Reached through confirmed dispatch links", ""
            }.Select(CsvCell)));
        }

        // 5. Possible scope customers
        foreach (var cust in result.PossibleCustomers)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                result.RunId.ToString(), result.InvestigationId?.ToString() ?? "", result.SelectedType, result.SelectedId,
                result.Direction, "POSSIBLE_CUSTOMER", "customer", cust, "", "", "PossibleDestination", "Candidate", "", "", "", "Requires operator review of upstream links", ""
            }.Select(CsvCell)));
        }

        // 6. Gaps
        foreach (var gap in result.Gaps)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                result.RunId.ToString(), result.InvestigationId?.ToString() ?? "", result.SelectedType, result.SelectedId,
                result.Direction, "GAP", "gap", "", "", "", gap.Title, "Gap", "", "", "", gap.Description, gap.Recommendation
            }.Select(CsvCell)));
        }

        // 7. Reconciliation findings
        foreach (var finding in result.Findings)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                result.RunId.ToString(), result.InvestigationId?.ToString() ?? "", result.SelectedType, result.SelectedId,
                result.Direction, "FINDING", finding.RelatedEntityType, finding.RelatedEntityId, "", finding.Scope, finding.Kind, finding.Severity, "", "", "", finding.Explanation, finding.EvidenceJson
            }.Select(CsvCell)));
        }

        // 8. Caveats
        foreach (var caveat in result.Caveats)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                result.RunId.ToString(), result.InvestigationId?.ToString() ?? "", result.SelectedType, result.SelectedId,
                result.Direction, "CAVEAT", "investigation", "", "", "", "IntegrityLimitation", "Caveat", "", "", "", caveat, ""
            }.Select(CsvCell)));
        }

        // 9. Audit trail
        if (audits != null)
        {
            foreach (var audit in audits)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    result.RunId.ToString(), result.InvestigationId?.ToString() ?? "", result.SelectedType, result.SelectedId,
                    result.Direction, "AUDIT", audit.TargetType, audit.TargetId, "", "", audit.EventType, audit.Actor, "", "", audit.CreatedAt.ToString("O"), audit.Description, audit.PayloadJson
                }.Select(CsvCell)));
            }
        }

        return sb.ToString();
    }

    /// <summary>CSV bytes with a UTF-8 BOM so spreadsheet tools detect the encoding on Windows.</summary>
    public static byte[] BuildCsvBytes(TraceResultDto result, IReadOnlyList<AuditEventDto>? audits = null)
    {
        var text = BuildCsv(result, audits);
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
    }

    public static byte[] BuildXlsx(TraceResultDto result, IReadOnlyList<AuditEventDto>? audits = null, IReadOnlyList<FactDto>? facts = null)
    {
        var auditList = audits ?? result.AuditTrail ?? [];
        var factList = facts ?? [];
        var disclaimer = ResolveDisclaimer(result);

        using var workbook = new XLWorkbook();
        workbook.Properties.Title = "Klarrow Trace & Evidence Reconciliation Workbook";
        workbook.Properties.Subject = "Food-traceability investigation export";
        workbook.Properties.Author = "Klarrow";
        workbook.Properties.Company = "Klarrow";
        workbook.Properties.Comments = "Generated from the Klarrow evidence-reconciliation workbench. Not a recall decision or regulatory clearance.";
        workbook.Properties.Created = DateTime.UtcNow;

        BuildSummarySheet(workbook, result, auditList, factList, disclaimer);
        BuildEvidenceSheet(workbook, factList);
        BuildTraceLinksSheet(workbook, result);
        BuildFindingsSheet(workbook, result);
        BuildAuditSheet(workbook, auditList);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void BuildSummarySheet(XLWorkbook workbook, TraceResultDto result, IReadOnlyList<AuditEventDto> audits, IReadOnlyList<FactDto> facts, string disclaimer)
    {
        var ws = workbook.Worksheets.Add("Investigation Summary");
        ws.TabColor = XLColor.FromHtml("#39466C");
        ws.Style.Font.FontName = "Segoe UI";
        ws.Style.Font.FontSize = 9;
        ws.Style.Font.FontColor = XLColor.FromHtml("#2D2F38");
        ws.Column(1).Width = 30;
        ws.Column(2).Width = 18;
        ws.Column(3).Width = 18;
        ws.Column(4).Width = 18;
        ws.Column(5).Width = 18;
        ws.Column(6).Width = 18;

        ws.Range(1, 1, 1, 6).Merge();
        ws.Cell(1, 1).Value = "Klarrow Trace & Evidence Reconciliation Workbook";
        ws.Range(1, 1, 1, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#39466C");
        ws.Range(1, 1, 1, 6).Style.Font.FontColor = XLColor.White;
        ws.Range(1, 1, 1, 6).Style.Font.Bold = true;
        ws.Range(1, 1, 1, 6).Style.Font.FontSize = 16;
        ws.Range(1, 1, 1, 6).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 28;

        ws.Range(2, 1, 2, 6).Merge();
        SetText(ws.Cell(2, 1), $"Generated {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  |  {result.InvestigationName} ({result.InvestigationReference})  |  Run {result.RunId}", false);
        ws.Range(2, 1, 2, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#F4F6FA");
        ws.Range(2, 1, 2, 6).Style.Font.FontColor = XLColor.FromHtml("#6B7280");
        ws.Row(2).Height = 22;

        ws.Range(3, 1, 3, 6).Merge();
        SetText(ws.Cell(3, 1), disclaimer, true);
        ws.Range(3, 1, 3, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#FBF3E0");
        ws.Range(3, 1, 3, 6).Style.Font.FontColor = XLColor.FromHtml("#7A4D12");
        ws.Range(3, 1, 3, 6).Style.Font.Bold = true;
        ws.Row(3).Height = 42;

        var row = 5;
        AddSummarySection(ws, ref row, "Case");
        AddSummaryRow(ws, ref row, "Investigation", result.InvestigationName);
        AddSummaryRow(ws, ref row, "Reference", result.InvestigationReference);
        AddSummaryRow(ws, ref row, "Run ID", result.RunId.ToString());
        AddSummaryRow(ws, ref row, "Selected lot / batch", $"{result.SelectedType} {result.SelectedId}");
        AddSummaryRow(ws, ref row, "Direction", result.Direction);
        AddSummaryRow(ws, ref row, "Completed (UTC)", result.CompletedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        AddSummaryRow(ws, ref row, "Workspace mode", string.IsNullOrWhiteSpace(result.WorkspaceMode) ? "not recorded" : result.WorkspaceMode);
        AddSummaryRow(ws, ref row, "Contains seeded synthetic links", result.ContainsSyntheticEvidence ? "Yes" : "No");

        AddSummarySection(ws, ref row, "Scope and review state");
        AddSummaryRow(ws, ref row, "Confirmed scope established", result.ConfirmedScopeEstablished ? "Yes" : "No");
        AddSummaryRow(ws, ref row, "Confirmed finished lots", result.AffectedFinishedLots.Count == 0 ? "None" : string.Join(", ", result.AffectedFinishedLots));
        AddSummaryRow(ws, ref row, "Confirmed customer destinations", result.AffectedCustomers.Count == 0 ? "None" : string.Join(", ", result.AffectedCustomers));
        AddSummaryRow(ws, ref row, "Possible finished lots", result.PossibleFinishedLots.Count == 0 ? "None" : string.Join(", ", result.PossibleFinishedLots));
        AddSummaryRow(ws, ref row, "Possible customer destinations", result.PossibleCustomers.Count == 0 ? "None" : string.Join(", ", result.PossibleCustomers));
        AddSummaryRow(ws, ref row, "Undecided relationship IDs", result.UndecidedRelationshipIds.Count.ToString(CultureInfo.InvariantCulture));
        AddSummaryRow(ws, ref row, "Open findings", result.Findings.Count(f => string.Equals(f.Status, "Open", StringComparison.OrdinalIgnoreCase)).ToString(CultureInfo.InvariantCulture));

        AddSummarySection(ws, ref row, "Mass balance");
        AddSummaryRow(ws, ref row, "Status", result.MassBalance.Status);
        AddSummaryRow(ws, ref row, "Input", QuantityText(result.MassBalance.InputTotal, result.MassBalance.InputUnit));
        AddSummaryRow(ws, ref row, "Output", QuantityText(result.MassBalance.OutputTotal, result.MassBalance.OutputUnit));
        AddSummaryRow(ws, ref row, "Waste", QuantityText(result.MassBalance.Waste, result.MassBalance.OutputUnit));
        AddSummaryRow(ws, ref row, "Notes", result.MassBalance.Notes);
        AddSummaryRow(ws, ref row, "Limitations", result.MassBalance.Limitations.Count == 0 ? "None recorded" : string.Join(" | ", result.MassBalance.Limitations));

        AddSummarySection(ws, ref row, "Export contents and provenance");
        AddSummaryRow(ws, ref row, "Fact rows in workbook", facts.Count.ToString(CultureInfo.InvariantCulture));
        AddSummaryRow(ws, ref row, "Trace links in workbook", result.Edges.Count.ToString(CultureInfo.InvariantCulture));
        AddSummaryRow(ws, ref row, "Findings in workbook", result.Findings.Count.ToString(CultureInfo.InvariantCulture));
        AddSummaryRow(ws, ref row, "Trace gaps in workbook", result.Gaps.Count.ToString(CultureInfo.InvariantCulture));
        AddSummaryRow(ws, ref row, "Audit events in workbook", audits.Count.ToString(CultureInfo.InvariantCulture));
        AddSummaryRow(ws, ref row, "Caveats", result.Caveats.Count == 0 ? "None recorded" : string.Join(" | ", result.Caveats));
        AddSummaryRow(ws, ref row, "Evidence boundary", "Fact grounding labels describe source matching, not semantic truth. Human confirmation is a recorded decision, not independent proof. Images/scans are not OCR'd in this build.");

        ws.Range(5, 1, Math.Max(row - 1, 5), 6).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
        ws.Range(5, 1, Math.Max(row - 1, 5), 6).Style.Border.BottomBorderColor = XLColor.FromHtml("#E5E8F0");
        ws.SheetView.FreezeRows(3);
        ApplyPrint(ws, row - 1, 6);
    }

    private static void BuildEvidenceSheet(XLWorkbook workbook, IReadOnlyList<FactDto> facts)
    {
        var ws = workbook.Worksheets.Add("Evidence & Facts");
        ws.TabColor = XLColor.FromHtml("#A0A6B4");
        var headers = new[]
        {
            "Source Document", "Locator", "Fact Type", "Claim Type", "Subject / Lot", "Raw Value", "Normalized Value",
            "Quantity", "Unit", "Event Date", "Origin", "Provider / Model", "Grounding Classification",
            "Source Match Method", "Review State", "Reviewer", "Review Reason", "Source Quote", "Supporting Excerpt"
        };
        var widths = new[] { 28d, 12d, 16d, 18d, 24d, 34d, 24d, 10d, 8d, 12d, 18d, 18d, 28d, 22d, 14d, 14d, 30d, 42d, 52d };
        var row = AddDataSheetHeader(ws, "Evidence & Facts", "Includes deterministic-parser and AI candidate facts. Grounding labels are not proof of semantic correctness.", headers);
        var start = row;
        foreach (var fact in facts)
        {
            SetText(ws.Cell(row, 1), fact.SourceDocumentName);
            SetText(ws.Cell(row, 2), fact.SourceLocation);
            SetText(ws.Cell(row, 3), fact.FactType.ToString());
            SetText(ws.Cell(row, 4), fact.ClaimType);
            SetText(ws.Cell(row, 5), fact.ClaimSubject);
            SetText(ws.Cell(row, 6), fact.RawValue);
            SetText(ws.Cell(row, 7), fact.NormalizedValue);
            SetNumber(ws.Cell(row, 8), fact.Quantity);
            SetText(ws.Cell(row, 9), fact.QuantityUnit);
            SetDate(ws.Cell(row, 10), fact.EventDate);
            SetText(ws.Cell(row, 11), fact.ExtractionMethod);
            SetText(ws.Cell(row, 12), $"{fact.Provider} / {fact.ModelId}".Trim(' ', '/'));
            SetText(ws.Cell(row, 13), GroundingLabel(fact.ValidationOutcome));
            SetText(ws.Cell(row, 14), fact.SourceMatchMethod);
            SetText(ws.Cell(row, 15), fact.ReviewStatus.ToString());
            SetText(ws.Cell(row, 16), fact.Reviewer);
            SetText(ws.Cell(row, 17), fact.ReviewReason);
            SetText(ws.Cell(row, 18), fact.SourceQuote);
            SetText(ws.Cell(row, 19), fact.EvidenceText);
            row++;
        }
        if (facts.Count == 0)
        {
            ws.Range(row, 1, row, headers.Length).Merge();
            SetText(ws.Cell(row, 1), "No facts in the selected investigation scope.");
            row++;
        }
        var last = Math.Max(row - 1, start);
        AddTable(ws, 3, last, headers.Length, "tblEvidence", widths);
        StyleDataRows(ws, 4, last, 48);
        ApplyPrint(ws, last, headers.Length, 2, false);
    }

    private static void BuildTraceLinksSheet(XLWorkbook workbook, TraceResultDto result)
    {
        var ws = workbook.Worksheets.Add("Trace Links");
        ws.TabColor = XLColor.FromHtml("#39466C");
        var headers = new[]
        {
            "From Type", "From ID", "Relationship", "To Type", "To ID", "State", "Quantity", "Unit",
            "Event Date", "Detection Method", "Origin", "Reviewed By", "Reviewed At", "Review Reason",
            "Needs Re-review", "Supporting Fact IDs", "Evidence", "Source References"
        };
        var widths = new[] { 16d, 24d, 22d, 16d, 24d, 13d, 10d, 8d, 12d, 20d, 20d, 14d, 18d, 34d, 12d, 34d, 50d, 50d };
        var row = AddDataSheetHeader(ws, "Trace Links", "Includes confirmed, candidate and rejected links for audit visibility. Rejected links are never traversed.", headers);
        var start = row;
        foreach (var edge in result.Edges)
        {
            SetText(ws.Cell(row, 1), edge.FromType);
            SetText(ws.Cell(row, 2), edge.FromId);
            SetText(ws.Cell(row, 3), edge.LinkKind);
            SetText(ws.Cell(row, 4), edge.ToType);
            SetText(ws.Cell(row, 5), edge.ToId);
            SetText(ws.Cell(row, 6), edge.State.ToString());
            SetNumber(ws.Cell(row, 7), edge.Quantity);
            SetText(ws.Cell(row, 8), edge.QuantityUnit);
            SetDate(ws.Cell(row, 9), edge.EventDate);
            SetText(ws.Cell(row, 10), edge.DetectionMethod);
            SetText(ws.Cell(row, 11), edge.Origin);
            SetText(ws.Cell(row, 12), edge.ReviewedBy);
            SetTimestamp(ws.Cell(row, 13), edge.ReviewedAt);
            SetText(ws.Cell(row, 14), edge.ReviewReason);
            SetText(ws.Cell(row, 15), edge.NeedsReReview ? "Yes" : "No");
            SetText(ws.Cell(row, 16), string.Join(", ", edge.SupportingFactIds));
            SetText(ws.Cell(row, 17), edge.EvidenceText);
            SetText(ws.Cell(row, 18), edge.SourceReferences);
            row++;
        }
        if (result.Edges.Count == 0)
        {
            ws.Range(row, 1, row, headers.Length).Merge();
            SetText(ws.Cell(row, 1), "No trace links in this snapshot.");
            row++;
        }
        var last = Math.Max(row - 1, start);
        AddTable(ws, 3, last, headers.Length, "tblTraceLinks", widths);
        StyleDataRows(ws, 4, last, 46);
        ApplyPrint(ws, last, headers.Length, 2, false);
    }

    private static void BuildFindingsSheet(XLWorkbook workbook, TraceResultDto result)
    {
        var ws = workbook.Worksheets.Add("Findings & Gaps");
        ws.TabColor = XLColor.FromHtml("#B7791F");
        var headers = new[]
        {
            "Record Type", "Kind", "Severity", "Status", "Title", "Explanation", "Related Type", "Related ID",
            "Scope", "Raised By", "Recommendation", "Evidence JSON"
        };
        var widths = new[] { 12d, 26d, 10d, 10d, 34d, 60d, 16d, 22d, 26d, 24d, 44d, 48d };
        var row = AddDataSheetHeader(ws, "Findings & Gaps", "Persisted findings and trace gaps for this investigation. Open status is shown as recorded.", headers);
        var start = row;
        foreach (var finding in result.Findings)
        {
            SetText(ws.Cell(row, 1), "Finding");
            SetText(ws.Cell(row, 2), finding.Kind);
            SetText(ws.Cell(row, 3), finding.Severity);
            SetText(ws.Cell(row, 4), finding.Status);
            SetText(ws.Cell(row, 5), finding.Title);
            SetText(ws.Cell(row, 6), finding.Explanation);
            SetText(ws.Cell(row, 7), finding.RelatedEntityType);
            SetText(ws.Cell(row, 8), finding.RelatedEntityId);
            SetText(ws.Cell(row, 9), finding.Scope);
            SetText(ws.Cell(row, 10), finding.RaisedByCheck);
            SetText(ws.Cell(row, 11), "");
            SetText(ws.Cell(row, 12), finding.EvidenceJson);
            row++;
        }
        foreach (var gap in result.Gaps)
        {
            SetText(ws.Cell(row, 1), "Trace gap");
            SetText(ws.Cell(row, 2), "missing-evidence");
            SetText(ws.Cell(row, 3), "");
            SetText(ws.Cell(row, 4), "Open");
            SetText(ws.Cell(row, 5), gap.Title);
            SetText(ws.Cell(row, 6), gap.Description);
            SetText(ws.Cell(row, 7), "");
            SetText(ws.Cell(row, 8), "");
            SetText(ws.Cell(row, 9), "");
            SetText(ws.Cell(row, 10), "trace");
            SetText(ws.Cell(row, 11), gap.Recommendation);
            SetText(ws.Cell(row, 12), "");
            row++;
        }
        if (result.Findings.Count == 0 && result.Gaps.Count == 0)
        {
            ws.Range(row, 1, row, headers.Length).Merge();
            SetText(ws.Cell(row, 1), "No findings or trace gaps in this snapshot.");
            row++;
        }
        var last = Math.Max(row - 1, start);
        AddTable(ws, 3, last, headers.Length, "tblFindingsGaps", widths);
        StyleDataRows(ws, 4, last, 56);
        ApplyPrint(ws, last, headers.Length, 1, false);
    }

    private static void BuildAuditSheet(XLWorkbook workbook, IReadOnlyList<AuditEventDto> audits)
    {
        var ws = workbook.Worksheets.Add("Audit Trail");
        ws.TabColor = XLColor.FromHtml("#6B7280");
        var headers = new[] { "Timestamp (UTC)", "Event Type", "Actor", "Target Type", "Target ID", "Description", "Payload JSON" };
        var widths = new[] { 20d, 24d, 18d, 18d, 38d, 60d, 48d };
        var row = AddDataSheetHeader(ws, "Audit Trail", "Append-only workspace audit events included in this export (most recent 500).", headers);
        var start = row;
        foreach (var audit in audits)
        {
            SetTimestamp(ws.Cell(row, 1), audit.CreatedAt);
            SetText(ws.Cell(row, 2), audit.EventType);
            SetText(ws.Cell(row, 3), audit.Actor);
            SetText(ws.Cell(row, 4), audit.TargetType);
            SetText(ws.Cell(row, 5), audit.TargetId);
            SetText(ws.Cell(row, 6), audit.Description);
            SetText(ws.Cell(row, 7), audit.PayloadJson);
            row++;
        }
        if (audits.Count == 0)
        {
            ws.Range(row, 1, row, headers.Length).Merge();
            SetText(ws.Cell(row, 1), "No audit events recorded.");
            row++;
        }
        var last = Math.Max(row - 1, start);
        AddTable(ws, 3, last, headers.Length, "tblAudit", widths);
        StyleDataRows(ws, 4, last, 34);
        ApplyPrint(ws, last, headers.Length, 1, false);
    }

    private static int AddDataSheetHeader(IXLWorksheet ws, string title, string subtitle, string[] headers)
    {
        ws.Style.Font.FontName = "Segoe UI";
        ws.Style.Font.FontSize = 9;
        ws.Style.Font.FontColor = XLColor.FromHtml("#2D2F38");
        ws.Range(1, 1, 1, headers.Length).Merge();
        ws.Cell(1, 1).Value = title;
        ws.Range(1, 1, 1, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#39466C");
        ws.Range(1, 1, 1, headers.Length).Style.Font.FontColor = XLColor.White;
        ws.Range(1, 1, 1, headers.Length).Style.Font.Bold = true;
        ws.Range(1, 1, 1, headers.Length).Style.Font.FontSize = 15;
        ws.Range(1, 1, 1, headers.Length).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 26;

        ws.Range(2, 1, 2, headers.Length).Merge();
        SetText(ws.Cell(2, 1), subtitle, false);
        ws.Range(2, 1, 2, headers.Length).Style.Fill.BackgroundColor = XLColor.FromHtml("#F4F6FA");
        ws.Range(2, 1, 2, headers.Length).Style.Font.FontColor = XLColor.FromHtml("#6B7280");
        ws.Row(2).Height = 24;

        var headerRow = 3;
        for (var i = 0; i < headers.Length; i++)
        {
            SetText(ws.Cell(headerRow, i + 1), headers[i], true);
            ws.Cell(headerRow, i + 1).Style.Font.Bold = true;
            ws.Cell(headerRow, i + 1).Style.Font.FontColor = XLColor.White;
            ws.Cell(headerRow, i + 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#39466C");
            ws.Cell(headerRow, i + 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }
        ws.Row(headerRow).Height = 30;
        ws.SheetView.FreezeRows(3);
        return headerRow + 1;
    }

    private static void AddTable(IXLWorksheet ws, int headerRow, int lastRow, int columns, string name, double[] widths)
    {
        var range = ws.Range(headerRow, 1, lastRow, columns);
        var table = range.CreateTable(name);
        table.Theme = XLTableTheme.TableStyleLight9;
        table.ShowAutoFilter = true;
        table.ShowRowStripes = true;
        table.ShowHeaderRow = true;
        for (var i = 1; i <= columns; i++) ws.Column(i).Width = widths[i - 1];
        ws.Range(headerRow, 1, headerRow, columns).Style.Fill.BackgroundColor = XLColor.FromHtml("#39466C");
        ws.Range(headerRow, 1, headerRow, columns).Style.Font.FontColor = XLColor.White;
        ws.Range(headerRow, 1, headerRow, columns).Style.Font.Bold = true;
        ws.Range(headerRow, 1, headerRow, columns).Style.Alignment.WrapText = true;
        ws.Range(headerRow, 1, headerRow, columns).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
    }

    private static void StyleDataRows(IXLWorksheet ws, int firstRow, int lastRow, double minHeight)
    {
        if (lastRow < firstRow) return;
        var rows = ws.Rows(firstRow, lastRow);
        rows.Style.Alignment.WrapText = true;
        rows.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
        rows.Style.Font.FontSize = 8.5;

        for (var rowNumber = firstRow; rowNumber <= lastRow; rowNumber++)
        {
            var row = ws.Row(rowNumber);
            var estimatedHeight = minHeight;
            foreach (var cell in row.CellsUsed())
            {
                var value = cell.GetString();
                if (string.IsNullOrEmpty(value)) continue;

                var width = ws.Column(cell.Address.ColumnNumber).Width;
                var charsPerLine = Math.Max(8d, width * 1.05d);
                var lines = value
                    .Split('\n')
                    .Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / charsPerLine)));
                estimatedHeight = Math.Max(estimatedHeight, (lines * 11.5d) + 5d);
            }

            row.Height = Math.Min(180d, estimatedHeight);
        }
    }

    private static void ApplyPrint(IXLWorksheet ws, int lastRow, int lastColumn, int pagesWide = 1, bool repeatTitle = true)
    {
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PagesWide = pagesWide;
        ws.PageSetup.PagesTall = 0;
        ws.PageSetup.SetRowsToRepeatAtTop(repeatTitle ? 1 : 3, 3);
        ws.PageSetup.CenterHorizontally = true;
        ws.PageSetup.Margins.Left = 0.25;
        ws.PageSetup.Margins.Right = 0.25;
        ws.PageSetup.Margins.Top = 0.35;
        ws.PageSetup.Margins.Bottom = 0.35;
        ws.PageSetup.PrintAreas.Add(1, 1, lastRow, lastColumn);
    }

    private static void AddSummarySection(IXLWorksheet ws, ref int row, string title)
    {
        ws.Range(row, 1, row, 6).Merge();
        SetText(ws.Cell(row, 1), title.ToUpperInvariant(), false);
        ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromHtml("#E5E8F0");
        ws.Range(row, 1, row, 6).Style.Font.Bold = true;
        ws.Range(row, 1, row, 6).Style.Font.FontColor = XLColor.FromHtml("#39466C");
        ws.Row(row).Height = 20;
        row++;
    }

    private static void AddSummaryRow(IXLWorksheet ws, ref int row, string label, string? value)
    {
        SetText(ws.Cell(row, 1), label, true);
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Range(row, 2, row, 6).Merge();
        SetText(ws.Cell(row, 2), value, true);
        var length = value?.Length ?? 0;
        ws.Row(row).Height = length > 180 ? 52 : length > 110 ? 38 : length > 60 ? 28 : 20;
        row++;
    }

    private static string QuantityText(decimal? value, string? unit)
        => value.HasValue ? $"{value.Value.ToString("#,##0.###", CultureInfo.InvariantCulture)} {unit}".Trim() : "Not recorded";

    private static string GroundingLabel(string? outcome) => outcome?.ToLowerInvariant() switch
    {
        "quote_verified" => "Value in quote - verified",
        "context_match_value_adjacent" => "Adjacent-line context",
        "identifier_matched_quote_unverified" => "Identifier only - quote unverified",
        "needs_review_unverified_source" => "Unlocated source",
        "schema-validated" => "Schema valid only",
        _ => string.IsNullOrWhiteSpace(outcome) ? "Not recorded" : outcome
    };

    private static void SetText(IXLCell cell, string? value, bool wrap = true)
    {
        var text = value ?? string.Empty;
        cell.SetValue(text);
        if (text.Length > 0 && FormulaTriggers.Contains(text[0])) cell.Style.IncludeQuotePrefix = true;
        cell.Style.Alignment.WrapText = wrap;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
    }

    private static void SetNumber(IXLCell cell, decimal? value)
    {
        if (value.HasValue)
        {
            cell.Value = (double)value.Value;
            cell.Style.NumberFormat.Format = "#,##0.###";
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }
        else
        {
            SetText(cell, "Not recorded");
        }
    }

    private static void SetDate(IXLCell cell, DateTime? value)
    {
        if (value.HasValue)
        {
            cell.Value = value.Value;
            cell.Style.DateFormat.Format = "yyyy-mm-dd";
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }
        else
        {
            SetText(cell, "Not recorded");
        }
    }

    private static void SetTimestamp(IXLCell cell, DateTime? value)
    {
        if (value.HasValue)
        {
            cell.Value = value.Value;
            cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss";
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        }
        else
        {
            SetText(cell, "Not recorded");
        }
    }
    public static string BuildHtml(TraceResultDto result, IReadOnlyList<AuditEventDto>? audits = null)
    {
        var disclaimer = ResolveDisclaimer(result);
        var edges = string.Join("", result.Edges.Select(e => $"<tr><td>{Html(e.FromId)}</td><td>{Html(e.ToId)}</td><td>{Html(e.LinkKind)}</td><td><span class=\"state state-{e.State.ToString().ToLowerInvariant()}\">{Html(e.State.ToString())}</span>{(e.NeedsReReview ? " <span class=\"re-review\">(Needs Re-Review)</span>" : "")}</td><td>{Html($"{e.Quantity?.ToString(CultureInfo.InvariantCulture) ?? "unknown"} {e.QuantityUnit}")}</td><td>{Html(e.EvidenceText)}</td><td>{Html(e.SourceReferences)}</td></tr>"));
        var gaps = string.Join("", result.Gaps.Select(g => $"<li><strong>{Html(g.Title)}</strong>: {Html(g.Description)} - <em>{Html(g.Recommendation)}</em></li>"));
        var findings = string.Join("", result.Findings.Select(f => $"<li><strong>{Html(f.Title)}</strong> ({Html(f.Severity)}, {Html(f.Kind)}): {Html(f.Explanation)}</li>"));
        var caveats = result.Caveats.Count == 0 ? "<p>No integrity caveats recorded for this run.</p>" : "<ul>" + string.Join("", result.Caveats.Select(c => $"<li>{Html(c)}</li>")) + "</ul>";
        var mbLimitations = result.MassBalance.Limitations.Count == 0 ? "" : "<ul>" + string.Join("", result.MassBalance.Limitations.Select(l => $"<li>{Html(l)}</li>")) + "</ul>";

        var confirmedFinished = result.AffectedFinishedLots.Count == 0 ? "<em>None reached via confirmed evidence links.</em>" : Html(string.Join(", ", result.AffectedFinishedLots));
        var confirmedCustomers = result.AffectedCustomers.Count == 0 ? "<em>None reached via confirmed dispatch records.</em>" : Html(string.Join(", ", result.AffectedCustomers));

        var possibleFinished = result.PossibleFinishedLots.Count == 0 ? "<em>None in possible scope.</em>" : Html(string.Join(", ", result.PossibleFinishedLots));
        var possibleCustomers = result.PossibleCustomers.Count == 0 ? "<em>None in possible scope.</em>" : Html(string.Join(", ", result.PossibleCustomers));

        var auditRows = audits == null ? "" : string.Join("", audits.Select(a => $"<tr><td>{Html(a.CreatedAt.ToString("O"))}</td><td>{Html(a.EventType)}</td><td>{Html(a.Actor)}</td><td>{Html(a.TargetType)}:{Html(a.TargetId)}</td><td>{Html(a.Description)}</td></tr>"));
        var auditSection = audits == null ? "" : $"<h2>Audit trail</h2><table><thead><tr><th>Time</th><th>Event</th><th>Actor</th><th>Target</th><th>Description</th></tr></thead><tbody>{auditRows}</tbody></table>";
        var css = "body{font-family:Segoe UI,Arial,sans-serif;margin:24px;color:#2d2f38;line-height:1.5;} table{border-collapse:collapse;width:100%;margin-bottom:16px;} th,td{border:1px solid #d6dbe5;padding:8px;font-size:13px;vertical-align:top;} th{background:#e5e8f0;text-align:left;} .watermark{background:#fef3c7;border:1px solid #f59e0b;color:#92400e;padding:12px;border-radius:6px;font-weight:700;margin-bottom:16px;} h1{margin-bottom:4px;color:#2d2f38;} h2{margin-top:20px;border-bottom:1px solid #d6dbe5;padding-bottom:4px;color:#2d2f38;} .state{font-weight:600;} .state-confirmed{color:#15803d;} .state-candidate{color:#b45309;} .state-inferred{color:#2563eb;} .re-review{color:#b91c1c;font-weight:600;}";

        return $"""
<!doctype html>
<html lang="en"><head><meta charset="utf-8"><title>Klarrow trace report</title>
<style>{css}</style>
</head><body>
<div class="watermark">{Html(disclaimer)}</div>
<h1>Klarrow Trace & Evidence Reconciliation Report</h1>
<p><strong>Run ID:</strong> {Html(result.RunId.ToString())} | <strong>Investigation:</strong> {Html(result.InvestigationName)} ({Html(result.InvestigationReference)}) | <strong>Selected:</strong> {Html(result.SelectedType)} {Html(result.SelectedId)} | <strong>Direction:</strong> {Html(result.Direction)} | <strong>Completed:</strong> {Html(result.CompletedAt.ToString("O"))}</p>
<p><strong>Workspace mode:</strong> {Html(string.IsNullOrWhiteSpace(result.WorkspaceMode) ? "unspecified" : result.WorkspaceMode)} | <strong>Synthetic evidence:</strong> {(result.ContainsSyntheticEvidence ? "Yes - contains seeded synthetic demonstration links; simulated records are identified at record level" : "No")}</p>
<p><strong>Summary:</strong> {Html(result.Summary)}</p>

<h2>Confirmed Affected Scope (Operator Verified)</h2>
<p><strong>Confirmed Finished Lots:</strong> {confirmedFinished}</p>
<p><strong>Confirmed Customer Destinations:</strong> {confirmedCustomers}</p>

<h2>Possible Exposure Scope (Pending Review / Unconfirmed)</h2>
<p><strong>Possible Finished Lots:</strong> {possibleFinished}</p>
<p><strong>Possible Customer Destinations:</strong> {possibleCustomers}</p>

<h2>Integrity Caveats & Limitations</h2>
{caveats}

<h2>Mass Balance Reconciliation</h2>
<p><strong>Status:</strong> {Html(result.MassBalance.Status)} | <strong>Notes:</strong> {Html(result.MassBalance.Notes)}</p>
{mbLimitations}

<h2>Traversed Evidence Links</h2>
<table><thead><tr><th>From</th><th>To</th><th>Kind</th><th>State</th><th>Quantity</th><th>Evidence</th><th>Source references</th></tr></thead><tbody>{edges}</tbody></table>

<h2>Missing Evidence Gaps</h2>
<ul>{gaps}</ul>

<h2>Reconciliation Findings</h2>
<ul>{findings}</ul>

{auditSection}

<p style="margin-top:24px;font-size:12px;color:#626b7b;"><em>Notice: Klarrow is an evidence reconciliation workbench. Uncertainty is displayed; absence of records in an ingested bundle does not prove absence of events. This report is an investigation aid, not an autonomous food-safety determination or regulatory clearance.</em></p>
</body></html>
""";
    }
}
