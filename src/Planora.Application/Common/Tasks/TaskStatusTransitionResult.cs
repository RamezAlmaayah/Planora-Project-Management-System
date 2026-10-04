using Planora.Domain.Enums;

namespace Planora.Application.Common.Tasks;

public sealed class TaskStatusTransitionResult
{
    public bool Succeeded { get; init; }
    public TaskItemStatus? Status { get; init; }
    public TaskStatusTransitionFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;

    public static TaskStatusTransitionResult Success(TaskItemStatus status) => new()
    {
        Succeeded = true,
        Status = status,
        Message = "Task status updated."
    };

    public static TaskStatusTransitionResult Reject(
        TaskStatusTransitionFailure failure, string message) => new()
    {
        Failure = failure,
        Message = message
    };
}
