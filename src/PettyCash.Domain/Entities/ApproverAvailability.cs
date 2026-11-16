namespace PettyCash.Domain.Entities;

public class ApproverAvailability
{
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;
    public bool IsOnLeave { get; set; }
    public DateTime CheckedAtUtc { get; set; }
    public string Source { get; set; } = string.Empty;
}
