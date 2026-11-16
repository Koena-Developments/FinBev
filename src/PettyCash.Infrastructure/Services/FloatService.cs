using Microsoft.EntityFrameworkCore;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;
using PettyCash.Domain;
using PettyCash.Domain.Enums;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public class FloatService(PettyCashDbContext db) : IFloatService
{
    public async Task<FloatSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var account = await db.FloatAccounts.AsNoTracking().FirstAsync(cancellationToken);
        var reserved = await FloatBalanceHelper.GetReservedAsync(db, cancellationToken);
        var available = FloatBalanceHelper.GetAvailable(account.Balance, reserved);
        return new FloatSummaryDto(account.Capacity, account.Balance, reserved, available);
    }

    public async Task ReplenishAsync(string adminUserId, ReplenishmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Enter a top-up amount greater than zero.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var account = await db.FloatAccounts.FirstAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"FloatAccounts\" WHERE \"Id\" = {account.Id} FOR UPDATE",
            cancellationToken);

        account = await db.FloatAccounts.FirstAsync(cancellationToken);
        var newBalance = account.Balance + request.Amount;
        if (newBalance > PettyCashPolicy.FloatCapacity)
        {
            throw new InvalidOperationException($"This top-up would take the petty cash float above its limit of R{PettyCashPolicy.FloatCapacity:N2}.");
        }

        account.Balance = newBalance;
        db.FloatLedgerEntries.Add(new Domain.Entities.FloatLedgerEntry
        {
            Id = Guid.NewGuid(),
            FloatAccountId = account.Id,
            EntryType = FloatLedgerEntryType.Replenishment,
            Amount = request.Amount,
            CreatedByUserId = adminUserId,
            CreatedAtUtc = DateTime.UtcNow,
            Note = request.Note
        });

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }
}
