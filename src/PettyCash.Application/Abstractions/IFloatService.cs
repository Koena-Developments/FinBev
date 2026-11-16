using PettyCash.Application.Dtos;

namespace PettyCash.Application.Abstractions;

public interface IFloatService
{
    Task<FloatSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
    Task ReplenishAsync(string adminUserId, ReplenishmentRequest request, CancellationToken cancellationToken = default);
}
