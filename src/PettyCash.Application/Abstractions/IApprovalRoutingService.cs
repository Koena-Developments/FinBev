using PettyCash.Domain.Entities;

namespace PettyCash.Application.Abstractions;

public sealed record RoutedApprover(string UserId, string? OnBehalfOfUserId, bool DelegateUnavailable);

public interface IApprovalRoutingService
{
    Task<RoutedApprover> ResolveAsync(ApprovalMatrix matrix, CancellationToken cancellationToken = default);
    Task<RoutedApprover> ResolveFinanceAsync(CancellationToken cancellationToken = default);
}
