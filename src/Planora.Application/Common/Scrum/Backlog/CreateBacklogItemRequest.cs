using Planora.Domain.Enums;

namespace Planora.Application.Common.Scrum.Backlog;

public sealed class CreateBacklogItemRequest
{
    public int ProjectId { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public PriorityLevel Priority { get; init; }

    public BacklogItemStatus Status { get; init; }

    public string CreatedByUserId { get; init; } = string.Empty;
}
