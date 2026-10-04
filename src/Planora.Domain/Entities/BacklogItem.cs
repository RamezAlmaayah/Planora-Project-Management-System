using Planora.Domain.Enums;

namespace Planora.Domain.Entities;

public class BacklogItem
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    public BacklogItemStatus Status { get; set; }
    = BacklogItemStatus.Draft;

    public string CreatedByUserId { get; set; } = string.Empty;

    public string? UpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;

    public ICollection<SprintBacklogItem> SprintAssignments { get; set; }
    = new List<SprintBacklogItem>();
}