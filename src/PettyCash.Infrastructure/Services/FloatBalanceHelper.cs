using Microsoft.EntityFrameworkCore;
using PettyCash.Domain.Enums;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

internal static class FloatBalanceHelper
{
    public static async Task<decimal> GetReservedAsync(PettyCashDbContext db, CancellationToken cancellationToken)
    {
        return await db.CashRequests
            .Where(r =>
                r.Status == RequestStatus.Submitted
                || r.Status == RequestStatus.Approved
                || r.Status == RequestStatus.PendingFinanceApproval
                || r.Status == RequestStatus.PendingDisbursement)
            .SumAsync(r => r.Amount, cancellationToken);
    }

    public static decimal GetAvailable(decimal balance, decimal reserved) => balance - reserved;
}
