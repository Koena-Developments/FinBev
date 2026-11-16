using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PettyCash.Application.Abstractions;
using PettyCash.Domain.Entities;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.HostedServices;

public class ApproverAvailabilitySyncHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<ApproverAvailabilitySyncHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Approver availability sync failed.");
                }

                await Task.Delay(TimeSpan.FromMinutes(15), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown (Ctrl+C, rebuild, etc.) — not a service failure.
        }
    }

    private async Task SyncAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PettyCashDbContext>();
        var leaveClient = scope.ServiceProvider.GetRequiredService<ILeaveStatusClient>();

        var matrices = await db.ApprovalMatrices.AsNoTracking().ToListAsync(cancellationToken);
        var approverIds = matrices
            .SelectMany(m => new[] { m.PrimaryApproverUserId, m.ActingApproverUserId })
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToList();

        var users = await db.Users.AsNoTracking()
            .Where(u => approverIds.Contains(u.Id) && u.EntraUpn != null && u.EntraUpn != "")
            .ToListAsync(cancellationToken);

        foreach (var user in users)
        {
            var result = await leaveClient.GetLeaveStatusAsync(user.EntraUpn, cancellationToken);
            if (!result.WasChecked)
            {
                continue;
            }

            var tracked = await db.ApproverAvailabilities.FindAsync([user.Id], cancellationToken);
            if (tracked is null)
            {
                db.ApproverAvailabilities.Add(new ApproverAvailability
                {
                    UserId = user.Id,
                    IsOnLeave = result.IsOnLeave,
                    CheckedAtUtc = DateTime.UtcNow,
                    Source = result.Source
                });
            }
            else
            {
                tracked.IsOnLeave = result.IsOnLeave;
                tracked.CheckedAtUtc = DateTime.UtcNow;
                tracked.Source = result.Source;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
