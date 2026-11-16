namespace PettyCash.Application.Dtos;

public sealed record DepartmentMonthStatsDto(
    Guid DepartmentId,
    string DepartmentName,
    int RequestCount,
    decimal DisbursedAmount,
    decimal InPipelineAmount);

public sealed record FinanceOverviewDto(
    int Year,
    int Month,
    FloatSummaryDto Float,
    decimal TotalDisbursedInMonth,
    int TotalRequestsInMonth,
    IReadOnlyList<DepartmentMonthStatsDto> Departments,
    DepartmentMonthStatsDto? TopDepartmentByRequests);
