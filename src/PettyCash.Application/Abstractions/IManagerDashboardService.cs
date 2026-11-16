using PettyCash.Application.Dtos;

namespace PettyCash.Application.Abstractions;

public interface IManagerDashboardService
{
    Task<ManagerOverviewDto> GetOverviewAsync(string userId, int year, int month, CancellationToken cancellationToken = default);
}
