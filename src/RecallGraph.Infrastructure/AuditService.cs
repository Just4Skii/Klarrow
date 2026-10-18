using Microsoft.EntityFrameworkCore;
using RecallGraph.Application;

namespace RecallGraph.Infrastructure;

public sealed class AuditService(RecallGraphDbContext db)
{
    public async Task AddAsync(Guid workspaceId, string eventType, string actor, string description, string targetType, string targetId, string payloadJson = "{}", CancellationToken ct = default)
    {
        db.AuditEvents.Add(new AuditEvent
        {
            WorkspaceId = workspaceId,
            EventType = eventType,
            Actor = string.IsNullOrWhiteSpace(actor) ? "reviewer" : actor,
            Description = description,
            TargetType = targetType,
            TargetId = targetId,
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<AuditEventDto>> ListAsync(Guid workspaceId, int take = 200, CancellationToken ct = default)
    {
        return await db.AuditEvents
            .Where(x => x.WorkspaceId == workspaceId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(take)
            .Select(x => new AuditEventDto
            {
                Id = x.Id,
                EventType = x.EventType,
                Actor = x.Actor,
                Description = x.Description,
                TargetType = x.TargetType,
                TargetId = x.TargetId,
                CreatedAt = x.CreatedAt,
                PayloadJson = x.PayloadJson
            })
            .ToListAsync(ct);
    }
}
