using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace RecallGraph.Infrastructure;

public sealed class SchemaInitializer(RecallGraphDbContext db, ILogger<SchemaInitializer> logger)
{
    private const int TargetVersion = 6;

    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);
        var version = await GetUserVersionAsync(ct);
        if (version >= TargetVersion) return;

        if (version < 1)
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS Investigations (
                    Id TEXT NOT NULL CONSTRAINT PK_Investigations PRIMARY KEY,
                    WorkspaceId TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Reference TEXT NOT NULL,
                    SuspectLotType TEXT NOT NULL,
                    SuspectLotId TEXT NOT NULL,
                    Description TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    CreatedBy TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                """, ct);
            if (!await HasColumnAsync("Workspaces", "IsDemo", ct))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE Workspaces ADD COLUMN IsDemo INTEGER NOT NULL DEFAULT 0;", ct);
            if (!await HasColumnAsync("TraceRuns", "InvestigationId", ct))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE TraceRuns ADD COLUMN InvestigationId TEXT NULL;", ct);
        }

        if (version < 2)
        {
            foreach (var column in new[] { "DerivedTextPath", "ProcessingStatus", "ParserName", "ParserVersion", "ProcessingError", "IsOriginal", "SupersededByDocumentId" })
            {
                if (!await HasColumnAsync("SourceDocuments", column, ct))
                {
                    var definition = column switch
                    {
                        "IsOriginal" => "INTEGER NOT NULL DEFAULT 1",
                        "SupersededByDocumentId" => "TEXT NULL",
                        _ => "TEXT NOT NULL DEFAULT ''"
                    };
                    await db.Database.ExecuteSqlRawAsync($"ALTER TABLE SourceDocuments ADD COLUMN {column} {definition};", ct);
                }
            }
            foreach (var column in new[] { "SubjectType", "SubjectValue", "Predicate", "ObjectType", "ObjectValue", "ValidationOutcome", "ExtractionMethod", "Provider", "ModelId", "PromptVersion", "ReviewReason" })
            {
                if (!await HasColumnAsync("ExtractedFacts", column, ct))
                    await db.Database.ExecuteSqlRawAsync($"ALTER TABLE ExtractedFacts ADD COLUMN {column} TEXT NOT NULL DEFAULT '';", ct);
            }
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS FactReviewEvents (
                    Id TEXT NOT NULL CONSTRAINT PK_FactReviewEvents PRIMARY KEY,
                    WorkspaceId TEXT NOT NULL,
                    ExtractedFactId TEXT NOT NULL,
                    Action TEXT NOT NULL,
                    PreviousValuesJson TEXT NOT NULL,
                    NewValuesJson TEXT NOT NULL,
                    Reviewer TEXT NOT NULL,
                    Reason TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
                """, ct);
        }

        if (version < 3)
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS GapFindings (
                    Id TEXT NOT NULL CONSTRAINT PK_GapFindings PRIMARY KEY,
                    WorkspaceId TEXT NOT NULL,
                    InvestigationId TEXT NULL,
                    TraceRunId TEXT NULL,
                    StableKey TEXT NOT NULL,
                    Kind TEXT NOT NULL,
                    Severity TEXT NOT NULL,
                    Title TEXT NOT NULL,
                    Explanation TEXT NOT NULL,
                    Scope TEXT NOT NULL,
                    RaisedByCheck TEXT NOT NULL,
                    RelatedEntityType TEXT NOT NULL,
                    RelatedEntityId TEXT NOT NULL,
                    EvidenceJson TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_GapFindings_Workspace_StableKey ON GapFindings (WorkspaceId, StableKey);
                """, ct);
        }

        if (version < 4)
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS MatchProposals (
                    Id TEXT NOT NULL CONSTRAINT PK_MatchProposals PRIMARY KEY,
                    WorkspaceId TEXT NOT NULL,
                    StableKey TEXT NOT NULL,
                    EntityType TEXT NOT NULL,
                    LeftValue TEXT NOT NULL,
                    RightValue TEXT NOT NULL,
                    Score TEXT NOT NULL,
                    ScoreKind TEXT NOT NULL,
                    SignalsJson TEXT NOT NULL,
                    State TEXT NOT NULL,
                    DecidedBy TEXT NOT NULL,
                    DecidedAt TEXT NULL,
                    Reason TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );
                CREATE TABLE IF NOT EXISTS EntityAliases (
                    Id TEXT NOT NULL CONSTRAINT PK_EntityAliases PRIMARY KEY,
                    WorkspaceId TEXT NOT NULL,
                    EntityType TEXT NOT NULL,
                    CanonicalValue TEXT NOT NULL,
                    AliasValue TEXT NOT NULL,
                    SourceDocumentId TEXT NULL,
                    SourceLocationId TEXT NULL,
                    ClaimId TEXT NULL,
                    ConfirmedBy TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
                CREATE INDEX IF NOT EXISTS IX_MatchProposals_Workspace_StableKey ON MatchProposals (WorkspaceId, StableKey);
                CREATE INDEX IF NOT EXISTS IX_EntityAliases_Workspace_CanonicalValue ON EntityAliases (WorkspaceId, CanonicalValue);
                """, ct);
        }

        if (version < 5)
        {
            if (!await HasColumnAsync("SourceDocuments", "InvestigationId", ct))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE SourceDocuments ADD COLUMN InvestigationId TEXT NULL;", ct);
            if (!await HasColumnAsync("TraceLinks", "InvestigationId", ct))
                await db.Database.ExecuteSqlRawAsync("ALTER TABLE TraceLinks ADD COLUMN InvestigationId TEXT NULL;", ct);
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_SourceDocuments_InvestigationId ON SourceDocuments (InvestigationId);", ct);
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_TraceLinks_InvestigationId ON TraceLinks (InvestigationId);", ct);
        }

        if (version < 6)
        {
            foreach (var column in new[] { "SourceQuote", "SourceMatchMethod" })
            {
                if (!await HasColumnAsync("ExtractedFacts", column, ct))
                    await db.Database.ExecuteSqlRawAsync($"ALTER TABLE ExtractedFacts ADD COLUMN {column} TEXT NOT NULL DEFAULT '';", ct);
            }
            var linkColumns = new (string Name, string Definition)[]
            {
                ("Origin", "TEXT NOT NULL DEFAULT 'legacy-manual'"),
                ("SupportingFactIdsJson", "TEXT NOT NULL DEFAULT '[]'"),
                ("Reason", "TEXT NOT NULL DEFAULT ''"),
                ("ConflictNote", "TEXT NOT NULL DEFAULT ''"),
                ("StableKey", "TEXT NOT NULL DEFAULT ''"),
                ("ReviewedBy", "TEXT NOT NULL DEFAULT ''"),
                ("ReviewedAt", "TEXT NULL"),
                ("ReviewReason", "TEXT NOT NULL DEFAULT ''"),
                ("NeedsReReview", "INTEGER NOT NULL DEFAULT 0"),
                ("Superseded", "INTEGER NOT NULL DEFAULT 0"),
                ("StaleReason", "TEXT NOT NULL DEFAULT ''")
            };
            foreach (var (name, definition) in linkColumns)
            {
                if (!await HasColumnAsync("TraceLinks", name, ct))
                    await db.Database.ExecuteSqlRawAsync($"ALTER TABLE TraceLinks ADD COLUMN {name} {definition};", ct);
            }
            await db.Database.ExecuteSqlRawAsync("CREATE INDEX IF NOT EXISTS IX_TraceLinks_StableKey ON TraceLinks (WorkspaceId, StableKey);", ct);
        }

        await SetUserVersionAsync(TargetVersion, ct);
        logger.LogInformation("RecallGraph schema upgraded to version {Version}", TargetVersion);
    }

    private async Task<int> GetUserVersionAsync(CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var value = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt32(value);
    }

    private async Task SetUserVersionAsync(int version, CancellationToken ct)
    {
        await db.Database.ExecuteSqlRawAsync($"PRAGMA user_version = {version};", ct);
    }

    private async Task<bool> HasColumnAsync(string table, string column, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await connection.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }
}
