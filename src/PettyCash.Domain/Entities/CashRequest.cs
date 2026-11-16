using PettyCash.Domain.Enums;

namespace PettyCash.Domain.Entities;

public class CashRequest
{
    public Guid Id { get; set; }
    public string ClaimantUserId { get; set; } = string.Empty;
    public AppUser Claimant { get; set; } = null!;
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public Guid BusinessUnitId { get; set; }
    public BusinessUnit BusinessUnit { get; set; } = null!;
    public Guid BudgetCodeId { get; set; }
    public BudgetCode BudgetCode { get; set; } = null!;
    public DateOnly ExpenseDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Draft;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public byte[]? ClaimantSignaturePng { get; set; }
    public DateTime? ClaimantSignedAtUtc { get; set; }
    public DateTime? FinanceFormReleasedAtUtc { get; set; }
    public ICollection<ApprovalTask> ApprovalTasks { get; set; } = [];
}
