using PettyCash.Application.Dtos;

namespace PettyCash.Application.Abstractions;

public interface IFinanceDashboardService
{
    Task<FinanceOverviewDto> GetOverviewAsync(int year, int month, CancellationToken cancellationToken = default);
}
