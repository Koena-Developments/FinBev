namespace PettyCash.Application.Dtos;

public sealed record ManagerStatusBreakdownDto(string Label, int Count, decimal Amount);

public sealed record ManagerTrendPointDto(int Day, int RequestCount, decimal Amount);

public sealed record ManagerRecentRequestDto(
    Guid Id,
    string Reason,
    decimal Amount,
    string Status,
    string ClaimantName,
    DateTime? SubmittedAtUtc,
    DateOnly ExpenseDate,
    string DepartmentName,
    DateTime CreatedAtUtc,
    string? ClaimantEmoji);

public sealed record ManagerOverviewDto(
    int Year,
    int Month,
    IReadOnlyList<string> ManagedDepartmentNames,
    int PendingMyApprovalCount,
    int TotalRequestsInMonth,
    decimal TotalRequestedAmountInMonth,
    decimal TotalDisbursedInMonth,
    IReadOnlyList<ManagerStatusBreakdownDto> StatusBreakdown,
    IReadOnlyList<ManagerTrendPointDto> DailyTrend,
    IReadOnlyList<ManagerRecentRequestDto> RecentRequests);
