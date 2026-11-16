using PettyCash.Domain.Enums;

namespace PettyCash.Domain.Entities;

public class ApprovalTask
{
    public Guid Id { get; set; }
    public Guid CashRequestId { get; set; }
    public CashRequest CashRequest { get; set; } = null!;
    public ApprovalStage Stage { get; set; }
    public string AssignedApproverUserId { get; set; } = string.Empty;
    public AppUser AssignedApprover { get; set; } = null!;
    public string? OnBehalfOfUserId { get; set; }
    public AppUser? OnBehalfOf { get; set; }
    public bool DelegateUnavailable { get; set; }
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;
    public string? Comment { get; set; }
    public string? SignatureHash { get; set; }
    public byte[]? SignatureImagePng { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
}
