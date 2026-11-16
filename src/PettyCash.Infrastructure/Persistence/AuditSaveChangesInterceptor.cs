using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PettyCash.Domain.Entities;
using PettyCash.Infrastructure.Identity;

namespace PettyCash.Infrastructure.Persistence;

public class AuditSaveChangesInterceptor(AuditUserContext auditUserContext) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is PettyCashDbContext db)
        {
            WriteAuditEntries(db);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void WriteAuditEntries(PettyCashDbContext db)
    {
        var actor = auditUserContext.UserId;
        var now = DateTime.UtcNow;

        foreach (var entry in db.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog || entry.State is EntityState.Detached or EntityState.Unchanged)
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => "Added",
                EntityState.Modified => "Modified",
                EntityState.Deleted => "Deleted",
                _ => "Unknown"
            };

            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                ActorUserId = actor,
                TimestampUtc = now,
                EntityType = entry.Entity.GetType().Name,
                EntityId = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey())?.CurrentValue?.ToString() ?? "",
                Action = action,
                BeforeJson = entry.State == EntityState.Modified ? JsonSerializer.Serialize(entry.OriginalValues.ToObject()) : null,
                AfterJson = entry.State != EntityState.Deleted ? JsonSerializer.Serialize(entry.CurrentValues.ToObject()) : null
            });
        }
    }
}
