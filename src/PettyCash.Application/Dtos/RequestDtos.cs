namespace PettyCash.Application.Dtos;

public sealed record CreateCashRequestDto(
    Guid DepartmentId,
    Guid BusinessUnitId,
    Guid BudgetCodeId,
    DateOnly ExpenseDate,
    string Reason,
    decimal Amount);

public sealed record CashRequestSummaryDto(
    Guid Id,
    string Reason,
    decimal Amount,
    string Status,
    DateOnly ExpenseDate,
    string DepartmentName,
    DateTime CreatedAtUtc);

public sealed record CashRequestDetailDto(
    Guid Id,
    string ClaimantName,
    string DepartmentName,
    string BusinessUnitName,
    string BudgetCode,
    DateOnly ExpenseDate,
    string Reason,
    decimal Amount,
    string Status,
    DateTime CreatedAtUtc,
    DateTime? SubmittedAtUtc,
    IReadOnlyList<ApprovalTaskDto> ApprovalTrail);

public sealed record ApprovalInboxItemDto(
    Guid TaskId,
    Guid RequestId,
    string Reason,
    decimal Amount,
    string Status,
    DateOnly ExpenseDate,
    string DepartmentName,
    string ClaimantName,
    string Stage,
    bool DelegateUnavailable,
    string? OnBehalfOfName,
    string? ClaimantEmoji);

/// <summary>
/// Row for the approvals queue UI. Display approval status is Approved only after Finance sign-off.
/// </summary>
public sealed record ApprovalQueueItemDto(
    Guid RequestId,
    string DisplayId,
    string Reason,
    decimal Amount,
    string DepartmentName,
    string ClaimantName,
    string DisplayApprovalStatus,
    DateOnly? DatePaid,
    DateTime? FinanceApprovedAtUtc,
    Guid? PendingTaskId,
    string? PendingStage,
    string? ClaimantEmoji);

public sealed record ApprovalTaskDto(
    Guid Id,
    string Stage,
    string AssignedApproverName,
    string? OnBehalfOfName,
    bool DelegateUnavailable,
    string Decision,
    string? Comment,
    DateTime? DecidedAtUtc);
