using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Sprints;

public sealed class SprintListItemViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Goal { get; init; } = string.Empty;
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public SprintStatus Status { get; init; }
    public int BacklogItemCount { get; init; }
    public int TaskCount { get; init; }
}
