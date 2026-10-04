namespace Planora.Web.ViewModels.Backlog;

public sealed class BacklogIndexViewModel
{
    public int ProjectId { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public bool CanManage { get; init; }

    public IReadOnlyList<BacklogItemListViewModel> Items { get; init; }
        = Array.Empty<BacklogItemListViewModel>();
}
