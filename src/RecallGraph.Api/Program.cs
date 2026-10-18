using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecallGraph.Api;
using RecallGraph.Application;
using RecallGraph.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://127.0.0.1:5173")
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

var app = builder.Build();

app.UseCors("frontend");
app.UseMiddleware<ApiKeyMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapControllers();
app.MapOpenApi();

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
