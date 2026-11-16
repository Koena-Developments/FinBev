using Microsoft.EntityFrameworkCore;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;
using PettyCash.Domain.Enums;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public class FinanceDashboardService(PettyCashDbContext db, IFloatService floatService) : IFinanceDashboardService
{
    public async Task<FinanceOverviewDto> GetOverviewAsync(int year, int month, CancellationToken cancellationToken = default)
    {
        if (month is < 1 or > 12)
        {
            throw new InvalidOperationException("Choose a month from January to December.");
        }

        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var submittedInMonth = await db.CashRequests.AsNoTracking()
            .Include(r => r.Department)
            .Where(r => r.SubmittedAtUtc >= monthStart && r.SubmittedAtUtc < monthEnd)
            .ToListAsync(cancellationToken);

        var disbursedRequestIds = await db.FloatLedgerEntries.AsNoTracking()
            .Where(e => e.EntryType == FloatLedgerEntryType.Disbursement
                && e.CreatedAtUtc >= monthStart
                && e.CreatedAtUtc < monthEnd
                && e.CashRequestId != null)
            .Select(e => e.CashRequestId!.Value)
            .ToListAsync(cancellationToken);

        var disbursedRequests = disbursedRequestIds.Count == 0
            ? []
            : await db.CashRequests.AsNoTracking()
                .Include(r => r.Department)
                .Where(r => disbursedRequestIds.Contains(r.Id))
                .ToListAsync(cancellationToken);

        var pipelineStatuses = new[]
        {
            RequestStatus.Submitted,
            RequestStatus.PendingFinanceApproval,
            RequestStatus.PendingDisbursement,
            RequestStatus.Approved
        };

        var departments = await db.Departments.AsNoTracking().OrderBy(d => d.Name).ToListAsync(cancellationToken);

        var stats = departments.Select(dept =>
        {
            var submitted = submittedInMonth.Where(r => r.DepartmentId == dept.Id).ToList();
            var disbursed = disbursedRequests.Where(r => r.DepartmentId == dept.Id).ToList();
            var inPipeline = submitted.Where(r => pipelineStatuses.Contains(r.Status)).Sum(r => r.Amount);

            return new DepartmentMonthStatsDto(
                dept.Id,
                dept.Name,
                submitted.Count,
                disbursed.Sum(r => r.Amount),
                inPipeline);
        }).Where(s => s.RequestCount > 0 || s.DisbursedAmount > 0 || s.InPipelineAmount > 0)
          .OrderByDescending(s => s.RequestCount)
          .ThenByDescending(s => s.DisbursedAmount)
          .ToList();

        var top = stats.OrderByDescending(s => s.RequestCount).FirstOrDefault();

        var floatSummary = await floatService.GetSummaryAsync(cancellationToken);

        return new FinanceOverviewDto(
            year,
            month,
            floatSummary,
            disbursedRequests.Sum(r => r.Amount),
            submittedInMonth.Count,
            stats,
            top is { RequestCount: > 0 } ? top : null);
    }
}
