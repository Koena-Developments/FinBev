namespace PettyCash.Application.Dtos;

public sealed record DepartmentDto(Guid Id, string Name);
public sealed record BusinessUnitDto(Guid Id, string Name, Guid DepartmentId);
public sealed record BudgetCodeDto(Guid Id, string Code, string Description, Guid BusinessUnitId);

public sealed record ApprovalMatrixDto(
    Guid Id,
    Guid DepartmentId,
    string DepartmentName,
    string PrimaryApproverUserId,
    string PrimaryApproverName,
    string? ActingApproverUserId,
    string? ActingApproverName);

public sealed record UpdateApprovalMatrixDto(
    string PrimaryApproverUserId,
    string? ActingApproverUserId);

public sealed record ApprovalDecisionRequest(string? Comment, string? SignatureImagePngBase64);
