namespace Planora.Application.Common.Tasks;

public sealed class TaskListResult
{
    public IReadOnlyList<TaskSummary> Items { get; init; } = Array.Empty<TaskSummary>();
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}
