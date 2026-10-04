namespace Planora.Application.Common.Tasks;

public sealed class TaskDetails : TaskSummary
{
    public int ProjectId { get; init; }
    public int SprintId { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public IReadOnlyList<QaReviewSummary> QaReviews { get; set; }
        = Array.Empty<QaReviewSummary>();
    public IReadOnlyList<TaskCommentSummary> Comments { get; set; }
        = Array.Empty<TaskCommentSummary>();
    public IReadOnlyList<TaskAttachmentSummary> Attachments { get; set; }
        = Array.Empty<TaskAttachmentSummary>();
}
