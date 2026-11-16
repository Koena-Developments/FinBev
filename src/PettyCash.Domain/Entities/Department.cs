namespace PettyCash.Domain.Entities;

public class Department
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ICollection<BusinessUnit> BusinessUnits { get; set; } = [];
    public ICollection<AppUser> Users { get; set; } = [];
    public ApprovalMatrix? ApprovalMatrix { get; set; }
}
