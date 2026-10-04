using Planora.Application.Common.Scrum.Sprints;
using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Sprints;

public sealed class SprintPlanningViewModel
{
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;

    public int SprintId { get; init; }
    public string SprintName { get; init; } = string.Empty;

    public SprintStatus SprintStatus { get; init; }
    public bool IsArchived { get; init; }

    public IReadOnlyList<SprintBacklogOption> ReadyItems { get; init; }
        = Array.Empty<SprintBacklogOption>();

    public IReadOnlyList<SprintBacklogItemSummary> SprintItems { get; init; }
        = Array.Empty<SprintBacklogItemSummary>();
}
