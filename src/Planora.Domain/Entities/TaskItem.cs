using Planora.Domain.Enums;

namespace Planora.Domain.Entities;

public class TaskItem
{
    public int Id { get; set; }

    public int SprintBacklogItemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    public TaskItemStatus Status { get; set; } = TaskItemStatus.ToDo;

    public string? AssignedUserId { get; set; }

    public DateTime? Deadline { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string? UpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public SprintBacklogItem SprintBacklogItem { get; set; } = null!;

    public ICollection<QaReview> QaReviews { get; set; }
    = new List<QaReview>();

    public ICollection<TaskAttachment> Attachments { get; set; }
    = new List<TaskAttachment>();

    public ICollection<TaskComment> Comments { get; set; }
    = new List<TaskComment>();

    public ICollection<Issue> Issues { get; set; }
    = new List<Issue>();
}