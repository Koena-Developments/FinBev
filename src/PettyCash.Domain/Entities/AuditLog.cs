namespace PettyCash.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public string? ActorUserId { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
