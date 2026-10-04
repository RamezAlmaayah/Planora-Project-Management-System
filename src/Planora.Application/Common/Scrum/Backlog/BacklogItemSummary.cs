using Planora.Domain.Enums;

namespace Planora.Application.Common.Scrum.Backlog;

public sealed class BacklogItemSummary
{
    public int Id { get; init; }

    public int ProjectId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public PriorityLevel Priority { get; init; }

    public BacklogItemStatus Status { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public bool IsAssignedToSprint { get; init; }
}
