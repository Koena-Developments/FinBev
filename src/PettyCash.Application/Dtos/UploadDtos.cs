namespace PettyCash.Application.Dtos;

public sealed record AssignUploadClaimRequest(Guid CashRequestId);

public sealed record UploadedFileDto(
    Guid Id,
    string FileName,
    string ContentType,
    long SizeBytes,
    DateTime CreatedAtUtc,
    Guid CashRequestId,
    string ClaimReference,
    string ClaimReason,
    string ClaimantName,
    string DepartmentName,
    decimal Amount,
    string Status);
