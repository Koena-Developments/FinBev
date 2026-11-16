using Microsoft.AspNetCore.Identity;

namespace PettyCash.Domain.Entities;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public Guid DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public bool MustChangePassword { get; set; }
    public string? EntraObjectId { get; set; }
    public string? EntraUpn { get; set; }
    /// <summary>One emoji from the staff keyboard, shown beside this person's name.</summary>
    public string? Emoji { get; set; }
}
