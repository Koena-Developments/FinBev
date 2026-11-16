namespace PettyCash.Domain.Entities;

public class ApprovalMatrix
{
    public Guid Id { get; set; }
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string PrimaryApproverUserId { get; set; } = string.Empty;
    public AppUser PrimaryApprover { get; set; } = null!;
    public string? ActingApproverUserId { get; set; }
    public AppUser? ActingApprover { get; set; }
}
