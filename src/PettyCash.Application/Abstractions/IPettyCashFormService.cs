using PettyCash.Application.Dtos;

namespace PettyCash.Application.Abstractions;

public interface IPettyCashFormService
{
    Task<bool> RequestExistsAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task<PettyCashFormDto?> GetFormAsync(Guid requestId, string userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PettyCashFormCopyItemDto>> ListFinanceCopiesAsync(string userId, CancellationToken cancellationToken = default);
    Task<byte[]> GenerateDocxAsync(Guid requestId, string userId, bool isAdmin, CancellationToken cancellationToken = default);

    /// <summary>System use (email attachments) — no user access check.</summary>
    Task<byte[]> GenerateDocxForNotificationAsync(Guid requestId, CancellationToken cancellationToken = default);
    Task ReleaseToFinanceCopiesAsync(Guid cashRequestId, CancellationToken cancellationToken = default);
}
