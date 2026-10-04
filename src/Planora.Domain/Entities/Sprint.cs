using Planora.Domain.Enums;

namespace Planora.Domain.Entities;

public class Sprint
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Goal { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public SprintStatus Status { get; set; }
        = SprintStatus.NotStarted;

    public string CreatedByUserId { get; set; } = string.Empty;

    public string? UpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;

    public ICollection<SprintBacklogItem> BacklogItems { get; set; }
        = new List<SprintBacklogItem>();
}
