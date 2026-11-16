using Microsoft.EntityFrameworkCore;
using PettyCash.Application.Abstractions;
using PettyCash.Domain;
using PettyCash.Domain.Entities;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public class ApprovalRoutingService(PettyCashDbContext db, ILeaveStatusClient leaveClient) : IApprovalRoutingService
{
    public async Task<RoutedApprover> ResolveFinanceAsync(CancellationToken cancellationToken = default)
    {
        var financeDept = await db.Departments.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Name == PettyCashPolicy.FinanceDepartmentName, cancellationToken)
            ?? throw new InvalidOperationException("Finance is not set up to receive this claim. Contact IT.");

        var matrix = await db.ApprovalMatrices
            .FirstOrDefaultAsync(m => m.DepartmentId == financeDept.Id, cancellationToken)
            ?? throw new InvalidOperationException("No Finance approver is set up for this claim. Contact Finance.");

        return await ResolveAsync(matrix, cancellationToken);
    }

    public async Task<RoutedApprover> ResolveAsync(ApprovalMatrix matrix, CancellationToken cancellationToken = default)
    {
        var primary = await db.Users.AsNoTracking().FirstAsync(u => u.Id == matrix.PrimaryApproverUserId, cancellationToken);
        var primaryLeave = await GetLeaveAsync(primary, cancellationToken);

        if (!primaryLeave)
        {
            return new RoutedApprover(matrix.PrimaryApproverUserId, null, false);
        }

        if (string.IsNullOrEmpty(matrix.ActingApproverUserId))
        {
            return new RoutedApprover(matrix.PrimaryApproverUserId, null, true);
        }

        var acting = await db.Users.AsNoTracking().FirstAsync(u => u.Id == matrix.ActingApproverUserId, cancellationToken);
        var actingLeave = await GetLeaveAsync(acting, cancellationToken);

        return new RoutedApprover(
            matrix.ActingApproverUserId,
            matrix.PrimaryApproverUserId,
            actingLeave);
    }

    private async Task<bool> GetLeaveAsync(AppUser user, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.EntraUpn))
        {
            return false;
        }

        var availability = await db.ApproverAvailabilities.AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == user.Id, cancellationToken);

        if (availability is not null && availability.CheckedAtUtc > DateTime.UtcNow.AddMinutes(-20))
        {
            return availability.IsOnLeave;
        }

        var result = await leaveClient.GetLeaveStatusAsync(user.EntraUpn, cancellationToken);
        if (!result.WasChecked)
        {
            return false;
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

        await db.SaveChangesAsync(cancellationToken);
        return result.IsOnLeave;
    }
}
