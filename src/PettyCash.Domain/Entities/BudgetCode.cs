namespace PettyCash.Domain.Entities;

public class BudgetCode
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid BusinessUnitId { get; set; }
    public BusinessUnit BusinessUnit { get; set; } = null!;
}
