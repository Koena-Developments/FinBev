namespace PettyCash.Domain.Entities;

/// <summary>Tracks the last receipt chase sent for a paid-out petty cash claim.</summary>
public class ReceiptReminder
{
    public Guid CashRequestId { get; set; }
    public CashRequest CashRequest { get; set; } = null!;
    public DateTime? EmployeeRemindedAtUtc { get; set; }
    public DateTime? ManagerRemindedAtUtc { get; set; }
    public int EmployeeOverdueSends { get; set; }
    public int ManagerOverdueSends { get; set; }
}
