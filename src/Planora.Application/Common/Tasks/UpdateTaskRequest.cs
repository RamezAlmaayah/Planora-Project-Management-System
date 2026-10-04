using Planora.Domain.Enums;

namespace Planora.Application.Common.Tasks;

public sealed class UpdateTaskRequest
{
    public int Id { get; init; }
    public int ProjectId { get; init; }
    public int SprintId { get; init; }
    public int SprintBacklogItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PriorityLevel Priority { get; init; }
    public string? AssignedUserId { get; init; }
    public DateTime? Deadline { get; init; }
    public string UpdatedByUserId { get; init; } = string.Empty;
}
