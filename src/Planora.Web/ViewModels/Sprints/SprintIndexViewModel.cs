namespace Planora.Web.ViewModels.Sprints;

public sealed class SprintIndexViewModel
{
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public bool CanManage { get; init; }
    public bool IsArchived { get; init; }

    public IReadOnlyList<SprintListItemViewModel> Sprints { get; init; }
        = Array.Empty<SprintListItemViewModel>();
}
