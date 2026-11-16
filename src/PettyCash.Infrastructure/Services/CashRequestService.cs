using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;
using PettyCash.Domain.Enums;
using PettyCash.Domain.Policy;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public class CashRequestService(
    PettyCashDbContext db,
    IApprovalRoutingService routingService,
    IRequestNotificationService notifications,
    ILogger<CashRequestService> logger) : ICashRequestService
{
    public async Task<IReadOnlyList<CashRequestSummaryDto>> ListMineAsync(string userId, CancellationToken cancellationToken = default)
    {
        var items = await db.CashRequests.AsNoTracking()
            .Include(r => r.Department)
            .Where(r => r.ClaimantUserId == userId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
        return items.Select(CashRequestMapper.ToSummary).ToList();
    }

    public async Task<CashRequestDetailDto?> GetAsync(
        Guid id,
        string userId,
        bool isAdmin,
        bool isApprover,
        CancellationToken cancellationToken = default)
    {
        var request = await LoadDetailQuery().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null)
        {
            return null;
        }

        if (!isAdmin && request.ClaimantUserId != userId)
        {
            if (!isApprover || !await CanApproverViewRequestAsync(request, userId, cancellationToken))
            {
                return null;
            }
        }

        return CashRequestMapper.ToDetail(request);
    }

    private async Task<bool> CanApproverViewRequestAsync(
        Domain.Entities.CashRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        var managesDepartment = await db.ApprovalMatrices.AsNoTracking()
            .AnyAsync(
                m => m.DepartmentId == request.DepartmentId
                    && (m.PrimaryApproverUserId == userId || m.ActingApproverUserId == userId),
                cancellationToken);

        if (managesDepartment)
        {
            return true;
        }

        return await db.ApprovalTasks.AsNoTracking()
            .AnyAsync(
                t => t.CashRequestId == request.Id && t.AssignedApproverUserId == userId,
                cancellationToken);
    }

    public async Task<CashRequestDetailDto> CreateDraftAsync(string userId, CreateCashRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (!RequestAmountValidator.IsValid(dto.Amount, out var error))
        {
            throw new InvalidOperationException(error);
        }

        await ValidateLookupsAsync(dto, cancellationToken);

        var claimant = await db.Users.AsNoTracking().FirstAsync(u => u.Id == userId, cancellationToken);
        if (claimant.DepartmentId != dto.DepartmentId)
        {
            throw new InvalidOperationException(
                "Petty cash must be claimed under your assigned department. You cannot select a different department.");
        }

        var entity = new Domain.Entities.CashRequest
        {
            Id = Guid.NewGuid(),
            ClaimantUserId = userId,
            DepartmentId = dto.DepartmentId,
            BusinessUnitId = dto.BusinessUnitId,
            BudgetCodeId = dto.BudgetCodeId,
            ExpenseDate = dto.ExpenseDate,
            Reason = dto.Reason.Trim(),
            Amount = dto.Amount,
            Status = RequestStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.CashRequests.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        return CashRequestMapper.ToDetail(await LoadDetailQuery().FirstAsync(r => r.Id == entity.Id, cancellationToken));
    }

    public async Task<CashRequestDetailDto> SubmitAsync(
        Guid id,
        string userId,
        SubmitCashRequestDto submit,
        CancellationToken cancellationToken = default)
    {
        var signature = DecodeSignature(submit.SignatureImagePngBase64);
        if (signature is null || signature.Length == 0)
        {
            throw new InvalidOperationException("Claimant signature is required on the petty cash form before submit.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var request = await db.CashRequests
            .Include(r => r.Department)
            .FirstOrDefaultAsync(r => r.Id == id && r.ClaimantUserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("This petty cash request could not be found.");

        if (request.Status != RequestStatus.Draft)
        {
            throw new InvalidOperationException("This request has already left draft, so it cannot be submitted again.");
        }

        if (!RequestAmountValidator.IsValid(request.Amount, out var amountError))
        {
            throw new InvalidOperationException(amountError);
        }

        var account = await db.FloatAccounts.FirstAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"FloatAccounts\" WHERE \"Id\" = {account.Id} FOR UPDATE",
            cancellationToken);

        account = await db.FloatAccounts.FirstAsync(cancellationToken);
        var reserved = await FloatBalanceHelper.GetReservedAsync(db, cancellationToken);
        var available = FloatBalanceHelper.GetAvailable(account.Balance, reserved);
        if (request.Amount > available)
        {
            throw new InvalidOperationException("There is not enough unreserved money in the petty cash float to cover this request.");
        }

        var matrix = await db.ApprovalMatrices
            .FirstOrDefaultAsync(m => m.DepartmentId == request.DepartmentId, cancellationToken)
            ?? throw new InvalidOperationException("No department senior manager is set up to approve claims for this department. Contact Finance.");

        RoutedApprover routed;
        try
        {
            routed = await routingService.ResolveAsync(matrix, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            throw;
        }

        request.Status = RequestStatus.Submitted;
        request.SubmittedAtUtc = DateTime.UtcNow;
        request.ClaimantSignaturePng = signature;
        request.ClaimantSignedAtUtc = DateTime.UtcNow;

        db.FloatLedgerEntries.Add(new Domain.Entities.FloatLedgerEntry
        {
            Id = Guid.NewGuid(),
            FloatAccountId = account.Id,
            EntryType = FloatLedgerEntryType.Reservation,
            Amount = request.Amount,
            CashRequestId = request.Id,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            Note = "Submit reservation"
        });

        db.ApprovalTasks.Add(new Domain.Entities.ApprovalTask
        {
            Id = Guid.NewGuid(),
            CashRequestId = request.Id,
            Stage = ApprovalStage.Department,
            AssignedApproverUserId = routed.UserId,
            OnBehalfOfUserId = routed.OnBehalfOfUserId,
            DelegateUnavailable = routed.DelegateUnavailable,
            Decision = ApprovalDecision.Pending
        });

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        try
        {
            await notifications.NotifySubmittedAsync(id, userId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Submit notifications failed for request {RequestId}", id);
        }

        return CashRequestMapper.ToDetail(await LoadDetailQuery().FirstAsync(r => r.Id == id, cancellationToken));
    }

    public async Task CancelAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var request = await db.CashRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.ClaimantUserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("This petty cash request could not be found.");

        if (request.Status != RequestStatus.Submitted)
        {
            throw new InvalidOperationException("Only a request that is waiting for approval can be cancelled.");
        }

        var account = await db.FloatAccounts.FirstAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"FloatAccounts\" WHERE \"Id\" = {account.Id} FOR UPDATE",
            cancellationToken);

        request.Status = RequestStatus.Cancelled;

        db.FloatLedgerEntries.Add(new Domain.Entities.FloatLedgerEntry
        {
            Id = Guid.NewGuid(),
            FloatAccountId = account.Id,
            EntryType = FloatLedgerEntryType.Release,
            Amount = request.Amount,
            CashRequestId = request.Id,
            CreatedByUserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            Note = "Cancel release"
        });

        var task = await db.ApprovalTasks
            .Where(t => t.CashRequestId == request.Id && t.Decision == ApprovalDecision.Pending)
            .OrderBy(t => t.Stage)
            .FirstOrDefaultAsync(cancellationToken);
        if (task is not null)
        {
            task.Decision = ApprovalDecision.Rejected;
            task.Comment = "Cancelled by claimant";
            task.DecidedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private async Task ValidateLookupsAsync(CreateCashRequestDto dto, CancellationToken cancellationToken)
    {
        var bu = await db.BusinessUnits.AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == dto.BusinessUnitId && b.DepartmentId == dto.DepartmentId, cancellationToken);
        if (bu is null)
        {
            throw new InvalidOperationException("That business unit does not belong to the department on this request.");
        }

        var code = await db.BudgetCodes.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == dto.BudgetCodeId && c.BusinessUnitId == dto.BusinessUnitId, cancellationToken);
        if (code is null)
        {
            throw new InvalidOperationException("That budget code does not belong to the business unit on this request.");
        }
    }

    public async Task<IReadOnlyList<CashRequestSummaryDto>> ListPendingDisbursementAsync(CancellationToken cancellationToken = default)
    {
        var items = await db.CashRequests.AsNoTracking()
            .Include(r => r.Department)
            .Where(r => r.Status == RequestStatus.PendingDisbursement)
            .OrderBy(r => r.SubmittedAtUtc)
            .ToListAsync(cancellationToken);
        return items.Select(CashRequestMapper.ToSummary).ToList();
    }

    public async Task DisburseAsync(Guid id, string userId, CancellationToken cancellationToken = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var request = await db.CashRequests
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw new InvalidOperationException("This petty cash request could not be found.");

        if (request.Status != RequestStatus.PendingDisbursement)
        {
            throw new InvalidOperationException("This request is not approved for cash to be paid out yet.");
        }

        var account = await db.FloatAccounts.FirstAsync(cancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"FloatAccounts\" WHERE \"Id\" = {account.Id} FOR UPDATE",
            cancellationToken);

        account = await db.FloatAccounts.FirstAsync(cancellationToken);
        if (request.Amount > account.Balance)
        {
            throw new InvalidOperationException("The petty cash float does not have enough money to pay this request.");
        }

        account.Balance -= request.Amount;
        request.Status = RequestStatus.Disbursed;

        var now = DateTime.UtcNow;
        db.FloatLedgerEntries.Add(new Domain.Entities.FloatLedgerEntry
        {
            Id = Guid.NewGuid(),
            FloatAccountId = account.Id,
            EntryType = FloatLedgerEntryType.Disbursement,
            Amount = request.Amount,
            CashRequestId = request.Id,
            CreatedByUserId = userId,
            CreatedAtUtc = now,
            Note = "Petty cash disbursed"
        });

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    private static byte[]? DecodeSignature(string? base64)
    {
        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        var payload = base64.Trim();
        var comma = payload.IndexOf(',');
        if (comma >= 0)
        {
            payload = payload[(comma + 1)..];
        }

        try
        {
            return Convert.FromBase64String(payload);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private IQueryable<Domain.Entities.CashRequest> LoadDetailQuery() =>
        db.CashRequests.AsNoTracking()
            .Include(r => r.Claimant)
            .Include(r => r.Department)
            .Include(r => r.BusinessUnit)
            .Include(r => r.BudgetCode)
            .Include(r => r.ApprovalTasks).ThenInclude(t => t.AssignedApprover)
            .Include(r => r.ApprovalTasks).ThenInclude(t => t.OnBehalfOf);
}
