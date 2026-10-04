namespace Planora.Domain.Entities;

public class SprintBacklogItem
{
    public int Id { get; set; }

    public int SprintId { get; set; }

    public int BacklogItemId { get; set; }

    public string AddedByUserId { get; set; } = string.Empty;

    public DateTime AddedAt { get; set; }

    public Sprint Sprint { get; set; } = null!;

    public BacklogItem BacklogItem { get; set; } = null!;

    public ICollection<TaskItem> Tasks { get; set; }
    = new List<TaskItem>();
}