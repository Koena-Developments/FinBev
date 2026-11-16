using PettyCash.Application.Dtos;

namespace PettyCash.Application.Abstractions;

public interface ICashRequestService
{
    Task<IReadOnlyList<CashRequestSummaryDto>> ListMineAsync(string userId, CancellationToken cancellationToken = default);
    Task<CashRequestDetailDto?> GetAsync(
        Guid id,
        string userId,
        bool isAdmin,
        bool isApprover,
        CancellationToken cancellationToken = default);
    Task<CashRequestDetailDto> CreateDraftAsync(string userId, CreateCashRequestDto dto, CancellationToken cancellationToken = default);
    Task<CashRequestDetailDto> SubmitAsync(
        Guid id,
        string userId,
        SubmitCashRequestDto submit,
        CancellationToken cancellationToken = default);
    Task CancelAsync(Guid id, string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashRequestSummaryDto>> ListPendingDisbursementAsync(CancellationToken cancellationToken = default);
    Task DisburseAsync(Guid id, string userId, CancellationToken cancellationToken = default);
}
