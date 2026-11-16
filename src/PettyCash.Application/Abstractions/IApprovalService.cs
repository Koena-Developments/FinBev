using PettyCash.Application.Dtos;

namespace PettyCash.Application.Abstractions;

public interface IApprovalService
{
    Task<IReadOnlyList<ApprovalInboxItemDto>> GetInboxAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApprovalQueueItemDto>> GetQueueAsync(string userId, CancellationToken cancellationToken = default);
    Task ApproveAsync(Guid taskId, string userId, ApprovalDecisionRequest request, CancellationToken cancellationToken = default);
    Task RejectAsync(Guid taskId, string userId, ApprovalDecisionRequest request, CancellationToken cancellationToken = default);
}
