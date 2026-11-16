namespace PettyCash.Domain.Entities;

public class FloatAccount
{
    public Guid Id { get; set; }
    public decimal Capacity { get; set; }
    public decimal Balance { get; set; }
    public ICollection<FloatLedgerEntry> LedgerEntries { get; set; } = [];
}
