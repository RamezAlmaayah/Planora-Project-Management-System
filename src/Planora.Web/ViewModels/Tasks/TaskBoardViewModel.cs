using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Tasks;

public sealed class TaskBoardViewModel
{
    public int ProjectId { get; init; }
    public int SprintId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string SprintName { get; init; } = string.Empty;
    public bool IsReadOnly { get; init; }
    public IReadOnlyList<TaskBoardCardViewModel> Tasks { get; init; }
        = Array.Empty<TaskBoardCardViewModel>();
}

public sealed class TaskBoardCardViewModel
{
    public int Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string BacklogTitle { get; init; } = string.Empty;
    public PriorityLevel Priority { get; init; }
    public TaskItemStatus Status { get; init; }
    public string? AssigneeName { get; init; }
    public DateTime? Deadline { get; init; }
    public bool CanTransition { get; init; }
    public bool CanQaReview { get; init; }
}
