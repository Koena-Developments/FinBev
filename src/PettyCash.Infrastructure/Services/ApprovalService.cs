using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PettyCash.Application.Abstractions;
using PettyCash.Application.Dtos;
using PettyCash.Domain.Enums;
using PettyCash.Infrastructure.Persistence;

namespace PettyCash.Infrastructure.Services;

public class ApprovalService(
    PettyCashDbContext db,
    IApprovalRoutingService routingService,
    IRequestNotificationService notifications,
    ILogger<ApprovalService> logger) : IApprovalService
{
    public async Task<IReadOnlyList<ApprovalInboxItemDto>> GetInboxAsync(string userId, CancellationToken cancellationToken = default)
    {
        var tasks = await db.ApprovalTasks.AsNoTracking()
            .Where(t => t.AssignedApproverUserId == userId && t.Decision == ApprovalDecision.Pending)
            .Include(t => t.CashRequest).ThenInclude(r => r.Department)
            .Include(t => t.CashRequest).ThenInclude(r => r.Claimant)
            .Include(t => t.OnBehalfOf)
            .ToListAsync(cancellationToken);

        return tasks.Select(t => new ApprovalInboxItemDto(
            t.Id,
            t.CashRequestId,
            t.CashRequest.Reason,
            t.CashRequest.Amount,
            t.CashRequest.Status.ToString(),
            t.CashRequest.ExpenseDate,
            t.CashRequest.Department.Name,
            t.CashRequest.Claimant.DisplayName,
            t.Stage.ToString(),
            t.DelegateUnavailable,
            t.OnBehalfOf?.DisplayName,
            t.CashRequest.Claimant.Emoji)).ToList();
    }

    public async Task ApproveAsync(Guid taskId, string userId, ApprovalDecisionRequest request, CancellationToken cancellationToken = default)
    {
        await DecideAsync(taskId, userId, request, approved: true, cancellationToken);
    }

    public async Task RejectAsync(Guid taskId, string userId, ApprovalDecisionRequest request, CancellationToken cancellationToken = default)
    {
        await DecideAsync(taskId, userId, request, approved: false, cancellationToken);
    }

    private async Task DecideAsync(Guid taskId, string userId, ApprovalDecisionRequest request, bool approved, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

        var task = await db.ApprovalTasks
            .Include(t => t.CashRequest)
            .FirstOrDefaultAsync(t => t.Id == taskId, cancellationToken)
            ?? throw new InvalidOperationException("This approval could not be found. Refresh the page and try again.");

        if (task.AssignedApproverUserId != userId)
        {
            throw new UnauthorizedAccessException("This approval is assigned to someone else.");
        }

        if (task.Decision != ApprovalDecision.Pending)
        {
            throw new InvalidOperationException("This claim has already been approved or rejected.");
        }

        var signaturePng = SignatureDecoder.Decode(request.SignatureImagePngBase64);
        if (approved && task.Stage == ApprovalStage.Department)
        {
            if (signaturePng is null || signaturePng.Length == 0)
            {
                throw new InvalidOperationException("Department senior manager signature is required on the petty cash form.");
            }
        }

        var expectedStatus = task.Stage switch
        {
            ApprovalStage.Department => RequestStatus.Submitted,
            ApprovalStage.Finance => RequestStatus.PendingFinanceApproval,
            _ => throw new InvalidOperationException("This approval step is not recognised. Contact Finance.")
        };

        if (task.CashRequest.Status != expectedStatus)
        {
            throw new InvalidOperationException("This request is not waiting for your approval.");
        }

        var decidedAt = DateTime.UtcNow;
        task.Decision = approved ? ApprovalDecision.Approved : ApprovalDecision.Rejected;
        task.Comment = request.Comment?.Trim();
        task.DecidedAtUtc = decidedAt;
        task.SignatureImagePng = signaturePng;
        task.SignatureHash = ComputeSignatureHash(task.CashRequestId, task.CashRequest.Amount, userId, decidedAt);

        if (approved)
        {
            if (task.Stage == ApprovalStage.Department)
            {
                task.CashRequest.Status = RequestStatus.PendingFinanceApproval;
                var financeRoute = await routingService.ResolveFinanceAsync(cancellationToken);
                db.ApprovalTasks.Add(new Domain.Entities.ApprovalTask
                {
                    Id = Guid.NewGuid(),
                    CashRequestId = task.CashRequestId,
                    Stage = ApprovalStage.Finance,
                    AssignedApproverUserId = financeRoute.UserId,
                    OnBehalfOfUserId = financeRoute.OnBehalfOfUserId,
                    DelegateUnavailable = financeRoute.DelegateUnavailable,
                    Decision = ApprovalDecision.Pending
                });

                task.CashRequest.FinanceFormReleasedAtUtc = DateTime.UtcNow;
            }
            else
            {
                task.CashRequest.Status = RequestStatus.PendingDisbursement;
            }
        }
        else
        {
            task.CashRequest.Status = RequestStatus.Rejected;
            var account = await db.FloatAccounts.FirstAsync(cancellationToken);
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM \"FloatAccounts\" WHERE \"Id\" = {account.Id} FOR UPDATE",
                cancellationToken);

            db.FloatLedgerEntries.Add(new Domain.Entities.FloatLedgerEntry
            {
                Id = Guid.NewGuid(),
                FloatAccountId = account.Id,
                EntryType = FloatLedgerEntryType.Release,
                Amount = task.CashRequest.Amount,
                CashRequestId = task.CashRequest.Id,
                CreatedByUserId = userId,
                CreatedAtUtc = decidedAt,
                Note = "Reject release"
            });
        }

        var requestId = task.CashRequestId;
        var stage = task.Stage;
        var wasApproved = approved;

        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);

        try
        {
            if (wasApproved && stage == ApprovalStage.Department)
            {
                await notifications.NotifyDepartmentApprovedAsync(requestId, userId, cancellationToken);
            }
            else if (wasApproved && stage == ApprovalStage.Finance)
            {
                await notifications.NotifyFinanceApprovedAsync(requestId, userId, cancellationToken);
            }
            else if (!wasApproved)
            {
                var stageLabel = stage == ApprovalStage.Finance ? "Finance senior manager" : "department senior manager";
                await notifications.NotifyRejectedAsync(requestId, stageLabel, userId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Approval notification failed for request {RequestId}", requestId);
        }
    }

    public async Task<IReadOnlyList<ApprovalQueueItemDto>> GetQueueAsync(string userId, CancellationToken cancellationToken = default)
    {
        var managedDeptIds = await db.ApprovalMatrices.AsNoTracking()
            .Where(m => m.PrimaryApproverUserId == userId || m.ActingApproverUserId == userId)
            .Select(m => m.DepartmentId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var financeRoute = await routingService.ResolveFinanceAsync(cancellationToken);
        var isFinanceApprover = financeRoute.UserId == userId
            || (!string.IsNullOrEmpty(financeRoute.OnBehalfOfUserId) && financeRoute.OnBehalfOfUserId == userId);

        var visibleStatuses = new[]
        {
            RequestStatus.Submitted,
            RequestStatus.PendingFinanceApproval,
            RequestStatus.PendingDisbursement,
            RequestStatus.Disbursed,
            RequestStatus.Rejected
        };

        var cutoff = DateTime.UtcNow.AddMonths(-6);

        var requestsQuery = db.CashRequests.AsNoTracking()
            .Include(r => r.Department)
            .Include(r => r.Claimant)
            .Include(r => r.ApprovalTasks)
            .Where(r => visibleStatuses.Contains(r.Status));

        if (isFinanceApprover)
        {
            requestsQuery = requestsQuery.Where(r =>
                r.Status != RequestStatus.Disbursed || (r.SubmittedAtUtc != null && r.SubmittedAtUtc >= cutoff));
        }
        else if (managedDeptIds.Count > 0)
        {
            requestsQuery = requestsQuery.Where(r =>
                managedDeptIds.Contains(r.DepartmentId)
                && (r.Status != RequestStatus.Disbursed || (r.SubmittedAtUtc != null && r.SubmittedAtUtc >= cutoff)));
        }
        else
        {
            requestsQuery = requestsQuery.Where(r =>
                r.ApprovalTasks.Any(t => t.AssignedApproverUserId == userId));
        }

        var requests = await requestsQuery
            .OrderByDescending(r => r.SubmittedAtUtc ?? r.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var requestIds = requests.Select(r => r.Id).ToList();
        var paidAtByRequest = await db.FloatLedgerEntries.AsNoTracking()
            .Where(e => e.EntryType == FloatLedgerEntryType.Disbursement
                && e.CashRequestId != null
                && requestIds.Contains(e.CashRequestId.Value))
            .GroupBy(e => e.CashRequestId!.Value)
            .Select(g => new { RequestId = g.Key, PaidAtUtc = g.Max(e => e.CreatedAtUtc) })
            .ToDictionaryAsync(x => x.RequestId, x => x.PaidAtUtc, cancellationToken);

        return requests.Select(r =>
        {
            var financeTask = r.ApprovalTasks.FirstOrDefault(t => t.Stage == ApprovalStage.Finance);
            var financeApproved = financeTask?.Decision == ApprovalDecision.Approved
                || r.Status is RequestStatus.PendingDisbursement or RequestStatus.Disbursed;

            var displayStatus = r.Status == RequestStatus.Rejected
                ? "Rejected"
                : financeApproved
                    ? "Approved"
                    : "Pending";

            paidAtByRequest.TryGetValue(r.Id, out var paidAtUtc);
            DateOnly? datePaid = paidAtUtc == default ? null : DateOnly.FromDateTime(paidAtUtc);

            var pendingTask = r.ApprovalTasks.FirstOrDefault(t =>
                t.AssignedApproverUserId == userId && t.Decision == ApprovalDecision.Pending);

            return new ApprovalQueueItemDto(
                r.Id,
                r.Id.ToString("N")[..8].ToUpperInvariant(),
                r.Reason,
                r.Amount,
                r.Department.Name,
                r.Claimant.DisplayName,
                displayStatus,
                datePaid,
                financeTask?.Decision == ApprovalDecision.Approved ? financeTask.DecidedAtUtc : null,
                pendingTask?.Id,
                pendingTask?.Stage.ToString(),
                r.Claimant.Emoji);
        }).ToList();
    }

    private static string ComputeSignatureHash(Guid requestId, decimal amount, string approverId, DateTime decidedAtUtc)
    {
        var payload = $"{requestId}|{amount:F2}|{approverId}|{decidedAtUtc:O}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }
}
