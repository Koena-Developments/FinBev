using PettyCash.Application.Dtos;
using PettyCash.Domain.Entities;

namespace PettyCash.Infrastructure.Services;

internal static class CashRequestMapper
{
    public static CashRequestSummaryDto ToSummary(CashRequest r) => new(
        r.Id,
        r.Reason,
        r.Amount,
        r.Status.ToString(),
        r.ExpenseDate,
        r.Department.Name,
        r.CreatedAtUtc);

    public static CashRequestDetailDto ToDetail(CashRequest r) => new(
        r.Id,
        r.Claimant.DisplayName,
        r.Department.Name,
        r.BusinessUnit.Name,
        r.BudgetCode.Code,
        r.ExpenseDate,
        r.Reason,
        r.Amount,
        r.Status.ToString(),
        r.CreatedAtUtc,
        r.SubmittedAtUtc,
        r.ApprovalTasks
            .OrderBy(t => t.Stage)
            .ThenBy(t => t.DecidedAtUtc ?? DateTime.MaxValue)
            .Select(t => new ApprovalTaskDto(
                t.Id,
                t.Stage.ToString(),
                t.AssignedApprover?.DisplayName ?? "Unknown",
                t.OnBehalfOf?.DisplayName,
                t.DelegateUnavailable,
                t.Decision.ToString(),
                t.Comment,
                t.DecidedAtUtc))
            .ToList());
}
