using Planora.Domain.Enums;

namespace Planora.Application.Common.Tasks;

public sealed class TaskStatusTransitionRequest
{
    public int ProjectId { get; init; }
    public int SprintId { get; init; }
    public int TaskId { get; init; }
    public TaskItemStatus CurrentStatus { get; init; }
    public TaskItemStatus TargetStatus { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
    public TaskTransitionActor Actor { get; init; }
}
