namespace PettyCash.Application.Dtos;

public sealed record SubmitCashRequestDto(string? SignatureImagePngBase64);

public sealed record PettyCashFormDto(
    Guid RequestId,
    string DisplayId,
    string ClaimantName,
    string DepartmentName,
    string BusinessUnitName,
    string BudgetCode,
    decimal Amount,
    string Reason,
    DateOnly ExpenseDate,
    string Status,
    DateTime? ClaimantSignedAtUtc,
    string? ClaimantSignaturePngBase64,
    DateTime? DepartmentManagerSignedAtUtc,
    string? DepartmentManagerSignaturePngBase64,
    string? DepartmentManagerName,
    DateTime? FinanceManagerSignedAtUtc,
    string? FinanceManagerSignaturePngBase64,
    string? FinanceManagerName,
    bool ReleasedToFinanceCopies);

public sealed record PettyCashFormCopyItemDto(
    Guid RequestId,
    string DisplayId,
    string Reason,
    decimal Amount,
    string DepartmentName,
    string ClaimantName,
    DateTime ReleasedAtUtc);
