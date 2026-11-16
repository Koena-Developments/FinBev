using Microsoft.EntityFrameworkCore;
using PettyCash.Domain.Enums;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

internal static class FormAccessHelper
{
    public static async Task<bool> CanViewAsync(
        PettyCashDbContext db,
        Domain.Entities.CashRequest request,
        string userId,
        bool isAdmin,
        Func<CancellationToken, Task<bool>> isFinanceSeniorManagerAsync,
        Func<string, CancellationToken, Task<bool>> isFinanceCopyRecipientAsync,
        CancellationToken cancellationToken)
    {
        if (isAdmin || request.ClaimantUserId == userId)
        {
            return true;
        }

        if (request.Status is not RequestStatus.Draft and not RequestStatus.Cancelled)
        {
            if (await isFinanceSeniorManagerAsync(cancellationToken))
            {
                return true;
            }
        }

        if (await isFinanceCopyRecipientAsync(userId, cancellationToken)
            && request.FinanceFormReleasedAtUtc is not null)
        {
            return true;
        }

        if (await db.ApprovalTasks.AsNoTracking().AnyAsync(
                t => t.CashRequestId == request.Id
                    && (t.AssignedApproverUserId == userId || t.OnBehalfOfUserId == userId),
                cancellationToken))
        {
            return true;
        }

        var managesDepartment = await db.ApprovalMatrices.AsNoTracking()
            .AnyAsync(
                m => m.DepartmentId == request.DepartmentId
                    && (m.PrimaryApproverUserId == userId || m.ActingApproverUserId == userId),
                cancellationToken);

        return managesDepartment;
    }
}
