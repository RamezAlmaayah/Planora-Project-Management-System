using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Backlog;

public sealed class BacklogDeleteViewModel
{
    public int Id { get; init; }

    public int ProjectId { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public PriorityLevel Priority { get; init; }

    public BacklogItemStatus Status { get; init; }

    public bool IsAssignedToSprint { get; init; }
}
