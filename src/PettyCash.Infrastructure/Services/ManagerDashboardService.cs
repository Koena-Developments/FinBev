using Microsoft.EntityFrameworkCore;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;
using PettyCash.Domain.Enums;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public class ManagerDashboardService(PettyCashDbContext db) : IManagerDashboardService
{
    public async Task<ManagerOverviewDto> GetOverviewAsync(
        string userId,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12)
        {
            throw new InvalidOperationException("Choose a month from January to December.");
        }

        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var managedDeptIds = await db.ApprovalMatrices.AsNoTracking()
            .Where(m => m.PrimaryApproverUserId == userId || m.ActingApproverUserId == userId)
            .Select(m => m.DepartmentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (managedDeptIds.Count == 0)
        {
            var userDept = await db.Users.AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.DepartmentId)
                .FirstOrDefaultAsync(cancellationToken);
            if (userDept != Guid.Empty)
            {
                managedDeptIds.Add(userDept);
            }
        }

        var deptNames = await db.Departments.AsNoTracking()
            .Where(d => managedDeptIds.Contains(d.Id))
            .OrderBy(d => d.Name)
            .Select(d => d.Name)
            .ToListAsync(cancellationToken);

        var pendingCount = await db.ApprovalTasks.AsNoTracking()
            .CountAsync(
                t => t.AssignedApproverUserId == userId
                    && t.Decision == ApprovalDecision.Pending
                    && t.Stage == ApprovalStage.Department,
                cancellationToken);

        var requestsInMonth = managedDeptIds.Count == 0
            ? []
            : await db.CashRequests.AsNoTracking()
                .Include(r => r.Claimant)
                .Where(r => managedDeptIds.Contains(r.DepartmentId)
                    && r.SubmittedAtUtc >= monthStart
                    && r.SubmittedAtUtc < monthEnd)
                .ToListAsync(cancellationToken);

        var disbursedIds = await db.FloatLedgerEntries.AsNoTracking()
            .Where(e => e.EntryType == FloatLedgerEntryType.Disbursement
                && e.CreatedAtUtc >= monthStart
                && e.CreatedAtUtc < monthEnd
                && e.CashRequestId != null)
            .Select(e => e.CashRequestId!.Value)
            .ToListAsync(cancellationToken);

        var disbursedInScope = disbursedIds.Count == 0 || managedDeptIds.Count == 0
            ? []
            : await db.CashRequests.AsNoTracking()
                .Where(r => disbursedIds.Contains(r.Id) && managedDeptIds.Contains(r.DepartmentId))
                .ToListAsync(cancellationToken);

        var statusGroups = requestsInMonth
            .GroupBy(r => MapStatusLabel(r.Status))
            .Select(g => new ManagerStatusBreakdownDto(g.Key, g.Count(), g.Sum(x => x.Amount)))
            .OrderByDescending(s => s.Count)
            .ToList();

        var dailyTrend = Enumerable.Range(1, daysInMonth)
            .Select(day =>
            {
                var dayStart = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
                var dayEnd = dayStart.AddDays(1);
                var dayRequests = requestsInMonth
                    .Where(r => r.SubmittedAtUtc >= dayStart && r.SubmittedAtUtc < dayEnd)
                    .ToList();
                return new ManagerTrendPointDto(day, dayRequests.Count, dayRequests.Sum(r => r.Amount));
            })
            .ToList();

        var recent = managedDeptIds.Count == 0
            ? []
            : await db.CashRequests.AsNoTracking()
                .Where(r => managedDeptIds.Contains(r.DepartmentId) && r.SubmittedAtUtc != null)
                .OrderByDescending(r => r.SubmittedAtUtc)
                .Select(r => new ManagerRecentRequestDto(
                    r.Id,
                    r.Reason,
                    r.Amount,
                    r.Status.ToString(),
                    r.Claimant.DisplayName,
                    r.SubmittedAtUtc,
                    r.ExpenseDate,
                    r.Department.Name,
                    r.CreatedAtUtc,
                    r.Claimant.Emoji))
                .ToListAsync(cancellationToken);

        return new ManagerOverviewDto(
            year,
            month,
            deptNames,
            pendingCount,
            requestsInMonth.Count,
            requestsInMonth.Sum(r => r.Amount),
            disbursedInScope.Sum(r => r.Amount),
            statusGroups,
            dailyTrend,
            recent);
    }

    private static string MapStatusLabel(RequestStatus status) => status switch
    {
        RequestStatus.Submitted => "Awaiting your sign-off",
        RequestStatus.PendingFinanceApproval => "With Finance",
        RequestStatus.PendingDisbursement => "Ready to pay out",
        RequestStatus.Disbursed => "Disbursed",
        RequestStatus.Rejected => "Rejected",
        RequestStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };
}
