using PettyCash.Domain.Enums;

namespace PettyCash.Domain.Entities;

public class FloatLedgerEntry
{
    public Guid Id { get; set; }
    public Guid FloatAccountId { get; set; }
    public FloatAccount FloatAccount { get; set; } = null!;
    public FloatLedgerEntryType EntryType { get; set; }
    public decimal Amount { get; set; }
    public Guid? CashRequestId { get; set; }
    public CashRequest? CashRequest { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Note { get; set; }
}
