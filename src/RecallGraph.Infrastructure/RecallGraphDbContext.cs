using Microsoft.EntityFrameworkCore;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class RecallGraphDbContext : DbContext
{
    public RecallGraphDbContext(DbContextOptions<RecallGraphDbContext> options) : base(options) { }

    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<Investigation> Investigations => Set<Investigation>();
    public DbSet<SourceDocument> SourceDocuments => Set<SourceDocument>();
    public DbSet<SourceLocation> SourceLocations => Set<SourceLocation>();
    public DbSet<ExtractedFact> ExtractedFacts => Set<ExtractedFact>();
    public DbSet<FactReviewEvent> FactReviewEvents => Set<FactReviewEvent>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<RawMaterialLot> RawMaterialLots => Set<RawMaterialLot>();
    public DbSet<ProductionBatch> ProductionBatches => Set<ProductionBatch>();
    public DbSet<FinishedLot> FinishedLots => Set<FinishedLot>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<TraceLink> TraceLinks => Set<TraceLink>();
    public DbSet<TraceRun> TraceRuns => Set<TraceRun>();
    public DbSet<EntityAlias> EntityAliases => Set<EntityAlias>();
    public DbSet<MatchProposal> MatchProposals => Set<MatchProposal>();
    public DbSet<GapFinding> GapFindings => Set<GapFinding>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<AiExtractionRun> AiExtractionRuns => Set<AiExtractionRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Workspace>().HasKey(x => x.Id);
        modelBuilder.Entity<Investigation>().HasKey(x => x.Id);
        modelBuilder.Entity<SourceDocument>().HasKey(x => x.Id);
        modelBuilder.Entity<SourceLocation>().HasKey(x => x.Id);
        modelBuilder.Entity<ExtractedFact>().HasKey(x => x.Id);
        modelBuilder.Entity<FactReviewEvent>().HasKey(x => x.Id);
        modelBuilder.Entity<GapFinding>().HasKey(x => x.Id);
        modelBuilder.Entity<EntityAlias>().HasKey(x => x.Id);
        modelBuilder.Entity<MatchProposal>().HasKey(x => x.Id);
        modelBuilder.Entity<TraceLink>().HasKey(x => x.Id);
        modelBuilder.Entity<TraceRun>().HasKey(x => x.Id);
        modelBuilder.Entity<AuditEvent>().HasKey(x => x.Id);
        modelBuilder.Entity<AiExtractionRun>().HasKey(x => x.Id);
    }
}
