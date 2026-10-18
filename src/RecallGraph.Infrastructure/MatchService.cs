using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class MatchService(RecallGraphDbContext db, AuditService audit)
{
    public async Task<List<MatchProposalDto>> ListAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var rows = await db.MatchProposals.Where(x => x.WorkspaceId == workspaceId)
            .OrderBy(x => x.State).ThenByDescending(x => x.UpdatedAt)
            .ToListAsync(ct);
        return rows.Select(Map).ToList();
    }

    public async Task<List<EntityAliasDto>> AliasesAsync(Guid workspaceId, CancellationToken ct = default)
    {
        return await db.EntityAliases.Where(x => x.WorkspaceId == workspaceId)
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new EntityAliasDto
            {
                Id = x.Id,
                EntityType = x.EntityType,
                CanonicalValue = x.CanonicalValue,
                AliasValue = x.AliasValue,
                SourceDocumentId = x.SourceDocumentId,
                ConfirmedBy = x.ConfirmedBy,
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<List<MatchProposalDto>> RefreshAsync(Guid workspaceId, CancellationToken ct = default)
    {
        var canonical = await db.RawMaterialLots.Where(x => x.WorkspaceId == workspaceId).Select(x => x.Code).ToListAsync(ct);
        canonical.AddRange(await db.FinishedLots.Where(x => x.WorkspaceId == workspaceId).Select(x => x.Code).ToListAsync(ct));
        var evidenceCodes = (await db.ExtractedFacts.Where(x => x.WorkspaceId == workspaceId).Select(x => new { x.ClaimSubject, x.NormalizedValue }).ToListAsync(ct))
            .SelectMany(x => new[] { x.ClaimSubject, x.NormalizedValue })
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToUpperInvariant())
            .Where(x => x.StartsWith("RM") || x.StartsWith("FL") || x.StartsWith("PB"))
            .Distinct()
            .ToList();

        var existing = await db.MatchProposals.Where(x => x.WorkspaceId == workspaceId).ToDictionaryAsync(x => x.StableKey, ct);
        foreach (var canonicalValue in canonical.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var variant in evidenceCodes.Where(v => !string.Equals(v, canonicalValue, StringComparison.OrdinalIgnoreCase) && IdentifierNormalizer.IsNearMatch(v, canonicalValue)))
            {
                // Order-insensitive key: the pair (A,B) and (B,A) describe the same alias question and must
                // produce exactly one proposal, not one per direction.
                var stableKey = string.CompareOrdinal(canonicalValue, variant) <= 0
                    ? $"near-match:{canonicalValue}:{variant}"
                    : $"near-match:{variant}:{canonicalValue}";
                if (existing.ContainsKey(stableKey)) continue;
                var signals = new List<string> { "normalized identifier similarity" };
                var canonicalKey = IdentifierNormalizer.NearMatchKey(canonicalValue);
                var variantKey = IdentifierNormalizer.NearMatchKey(variant);
                if (canonicalKey == variantKey && !string.Equals(canonicalKey, canonicalValue, StringComparison.OrdinalIgnoreCase))
                    signals.Add("visually confusable characters such as O/0 or I/1 differ in the raw value");
                var proposal = new MatchProposal
                {
                    WorkspaceId = workspaceId,
                    StableKey = stableKey,
                    EntityType = canonicalValue.StartsWith("FL", StringComparison.OrdinalIgnoreCase) ? "finished-lot" : canonicalValue.StartsWith("PB", StringComparison.OrdinalIgnoreCase) ? "production-batch" : "raw-lot",
                    LeftValue = canonicalValue,
                    RightValue = variant,
                    Score = 0.5m,
                    ScoreKind = "heuristic-not-calibrated",
                    SignalsJson = JsonSerializer.Serialize(signals),
                    State = "Candidate"
                };
                db.MatchProposals.Add(proposal);
                existing[stableKey] = proposal;
            }
        }
        await db.SaveChangesAsync(ct);
        return await ListAsync(workspaceId, ct);
    }

    public async Task<MatchProposalDto> DecideAsync(Guid workspaceId, Guid id, string action, MatchDecisionDto decision, CancellationToken ct = default)
    {
        var proposal = await db.MatchProposals.FirstOrDefaultAsync(x => x.WorkspaceId == workspaceId && x.Id == id, ct) ?? throw new KeyNotFoundException("Match proposal not found.");
        var actor = string.IsNullOrWhiteSpace(decision.Actor) ? "operator" : decision.Actor;
        proposal.State = action switch
        {
            "confirm" => "Confirmed",
            "reject" => "Rejected",
            "defer" => "Deferred",
            _ => throw new InvalidOperationException("Unsupported match decision.")
        };
        proposal.DecidedBy = actor;
        proposal.DecidedAt = DateTime.UtcNow;
        proposal.Reason = decision.Reason ?? string.Empty;
        proposal.UpdatedAt = DateTime.UtcNow;
        if (proposal.State == "Confirmed" && !await db.EntityAliases.AnyAsync(x => x.WorkspaceId == workspaceId && x.CanonicalValue == proposal.LeftValue && x.AliasValue == proposal.RightValue, ct))
        {
            db.EntityAliases.Add(new EntityAlias
            {
                WorkspaceId = workspaceId,
                EntityType = proposal.EntityType,
                CanonicalValue = proposal.LeftValue,
                AliasValue = proposal.RightValue,
                ConfirmedBy = actor
            });
        }
        await db.SaveChangesAsync(ct);
        await audit.AddAsync(workspaceId, $"match_{proposal.State.ToLowerInvariant()}", actor, $"{proposal.State} match proposal between '{proposal.LeftValue}' and '{proposal.RightValue}'.", "MatchProposal", proposal.Id.ToString(), JsonSerializer.Serialize(new { proposal.LeftValue, proposal.RightValue, proposal.Reason }), ct);
        return Map(proposal);
    }

    private static MatchProposalDto Map(MatchProposal x) => new()
    {
        Id = x.Id,
        StableKey = x.StableKey,
        EntityType = x.EntityType,
        LeftValue = x.LeftValue,
        RightValue = x.RightValue,
        Score = x.Score,
        ScoreKind = x.ScoreKind,
        SignalsJson = x.SignalsJson,
        State = x.State,
        DecidedBy = x.DecidedBy,
        DecidedAt = x.DecidedAt,
        Reason = x.Reason,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };
}
