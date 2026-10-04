using Planora.Domain.Enums;

namespace Planora.Application.Common.Scrum.Sprints;

public sealed class SprintBacklogOption
{
    public int Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public PriorityLevel Priority { get; init; }

    public BacklogItemStatus Status { get; init; }
}
