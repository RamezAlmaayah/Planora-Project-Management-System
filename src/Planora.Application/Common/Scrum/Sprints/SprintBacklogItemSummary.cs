using Planora.Domain.Enums;

namespace Planora.Application.Common.Scrum.Sprints;

public sealed class SprintBacklogItemSummary
{
    public int Id { get; init; }
    public int SprintBacklogItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public PriorityLevel Priority { get; init; }
    public BacklogItemStatus Status { get; init; }
    public int TaskCount { get; init; }
}
