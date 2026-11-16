namespace PettyCash.Domain.Entities;

public class BusinessUnit
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public ICollection<BudgetCode> BudgetCodes { get; set; } = [];
}
