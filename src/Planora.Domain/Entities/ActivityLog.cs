namespace Planora.Domain.Entities;

public class ActivityLog
{
    public int Id { get; set; }

    public string ActorUserId { get; set; } = string.Empty;

    public int? ProjectId { get; set; }

    public string Action { get; set; } = string.Empty;

    public string ResourceType { get; set; } = string.Empty;

    public string ResourceId { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? TraceId { get; set; }

    public DateTime CreatedAt { get; set; }
}