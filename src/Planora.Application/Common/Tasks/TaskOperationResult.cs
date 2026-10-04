namespace Planora.Application.Common.Tasks;

public sealed class TaskOperationResult
{
    public bool Succeeded { get; init; }
    public int? TaskId { get; init; }
    public string? Error { get; init; }

    public static TaskOperationResult Success(int taskId) => new() { Succeeded = true, TaskId = taskId };
    public static TaskOperationResult Failure(string error) => new() { Error = error };
}
