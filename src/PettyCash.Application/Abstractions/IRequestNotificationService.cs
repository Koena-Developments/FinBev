namespace PettyCash.Application.Abstractions;

public interface IRequestNotificationService
{
    Task NotifySubmittedAsync(Guid cashRequestId, string senderUserId, CancellationToken cancellationToken = default);

    Task NotifyDepartmentApprovedAsync(Guid cashRequestId, string senderUserId, CancellationToken cancellationToken = default);

    Task NotifyFinanceApprovedAsync(Guid cashRequestId, string senderUserId, CancellationToken cancellationToken = default);

    Task NotifyRejectedAsync(Guid cashRequestId, string stageLabel, string senderUserId, CancellationToken cancellationToken = default);
}
