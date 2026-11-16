using PettyCash.Application.Dtos;

namespace PettyCash.Application.Abstractions;

public interface IUploadService
{
    Task<IReadOnlyList<UploadedFileDto>> ListMineAsync(string userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UploadedFileDto>> ListForFinanceAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlySet<Guid>> ListCashRequestIdsWithUploadsAsync(CancellationToken cancellationToken = default);

    Task<UploadedFileDto> SaveAsync(
        string userId,
        Guid cashRequestId,
        Stream content,
        string fileName,
        string? contentType,
        CancellationToken cancellationToken = default);

    Task<UploadedFileDto> AssignClaimAsync(
        string userId,
        Guid fileId,
        Guid cashRequestId,
        CancellationToken cancellationToken = default);

    Task<(UploadedFileDto Meta, Stream Content)?> OpenAsync(
        string userId,
        Guid id,
        bool asFinance,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string userId, Guid id, CancellationToken cancellationToken = default);
}
