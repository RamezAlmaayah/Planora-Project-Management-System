using Planora.Domain.Enums;

namespace Planora.Application.Common.Tasks;

public class TaskSummary
{
    public int Id { get; init; }
    public int SprintBacklogItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string BacklogTitle { get; init; } = string.Empty;
    public BacklogItemStatus BacklogStatus { get; init; }
    public PriorityLevel Priority { get; init; }
    public TaskItemStatus Status { get; init; }
    public string? AssignedUserId { get; init; }
    public string? AssigneeName { get; init; }
    public DateTime? Deadline { get; init; }
}
