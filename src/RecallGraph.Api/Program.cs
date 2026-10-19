using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecallGraph.Api;
using RecallGraph.Application;
using RecallGraph.Infrastructure;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    // Localhost origins are the default development pair; extra deployments (e.g. GraffGrid Pages)
    // append their origins through the "Cors:Origins" configuration section.
    var configuredOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
    var allowedOrigins = new[] { "http://localhost:5173", "http://localhost:5174", "http://127.0.0.1:5173" }
        .Concat(configuredOrigins)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection("Storage"));
var storageSection = builder.Configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
var dataRoot = storageSection.DataRoot;
if (!Path.IsPathRooted(dataRoot)) dataRoot = Path.Combine(builder.Environment.ContentRootPath, dataRoot);
Directory.CreateDirectory(dataRoot);
storageSection.DataRoot = dataRoot;
storageSection.UploadsPath = Path.IsPathRooted(storageSection.UploadsPath) ? storageSection.UploadsPath : Path.Combine(dataRoot, "uploads");
Directory.CreateDirectory(storageSection.UploadsPath);
var dbPath = Path.Combine(dataRoot, "recallgraph.db");
builder.Services.Configure<StorageOptions>(o =>
{
    o.DataRoot = dataRoot;
    o.UploadsPath = storageSection.UploadsPath;
});
builder.Services.AddDbContext<RecallGraphDbContext>(options => options.UseSqlite($"Data Source={dbPath}"));

builder.Services.AddScoped<WorkspaceService>();
builder.Services.AddScoped<InvestigationService>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddScoped<FactService>();
builder.Services.AddScoped<TraceService>();
builder.Services.AddScoped<GapEngine>();
builder.Services.AddScoped<MatchService>();
builder.Services.AddScoped<RelationshipService>();
builder.Services.AddScoped<SchemaInitializer>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<SeedService>();
builder.Services.AddScoped<ExtractionService>();
builder.Services.AddScoped<OllamaHealthService>();
builder.Services.AddSingleton(LlamaOptions.FromEnvironment());builder.Services.AddSingleton<IAiExtractionProvider>(sp => new OpenAiCompatibleLlamaProvider(new HttpClient(), sp.GetRequiredService<LlamaOptions>()));builder.Services.AddScoped<MetaCapabilityService>();
builder.Services.AddSingleton(storageSection);

// Abuse guard for public demo deployments: opt-in per-IP fixed-window limiter.
// Disabled by default (and in local development / the automated test harness) via RateLimit:Enabled.
if (builder.Configuration.GetValue("RateLimit:Enabled", false))
{
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue("RateLimit:PermitsPerMinute", 120),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
    });
}

var app = builder.Build();

// Engage the per-IP limiter only when opted in (public demo deployments). Local development and the
// automated test harness run with RateLimit:Enabled=false so high-volume integration tests never trip 429s.
if (app.Configuration.GetValue("RateLimit:Enabled", false))
{
    app.UseRateLimiter();
}

app.UseCors("frontend");
app.UseMiddleware<ApiKeyMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapOpenApi();

// Static SPA fallback: when this API deployment also serves the built React UI (Docker single-container
// mode), unmatched routes resolve to the SPA shell. API endpoints keep matching first.
app.MapFallbackToFile("index.html");

using (var scope = app.Services.CreateScope())
{
    var initializer = scope.ServiceProvider.GetRequiredService<SchemaInitializer>();
    await initializer.InitializeAsync();
    var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var demoEnabled = configuration.GetValue("Seed:DemoEnabled", true);
    var seed = scope.ServiceProvider.GetRequiredService<SeedService>();
    await seed.EnsureSeededAsync(demoEnabled);
}

app.Run();

public partial class Program;
