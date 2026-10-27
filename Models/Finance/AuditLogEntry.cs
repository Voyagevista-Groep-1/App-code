using System.ComponentModel.DataAnnotations;

namespace tui.Models;

public class AuditLogEntry
{
    [Key]
    public long AuditLogEntryId { get; set; }
    [MaxLength(50)]
    public string EntityType { get; set; } = string.Empty;
    public int EntityId { get; set; }
    [MaxLength(50)]
    public string Action { get; set; } = string.Empty;
    public int ActorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
}
