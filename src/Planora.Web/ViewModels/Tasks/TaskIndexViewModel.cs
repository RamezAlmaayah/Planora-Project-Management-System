using Planora.Application.Common.Tasks;

namespace Planora.Web.ViewModels.Tasks;

public sealed class TaskIndexViewModel
{
    public int ProjectId { get; init; }
    public int SprintId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string SprintName { get; init; } = string.Empty;
    public bool CanManage { get; init; }
    public bool IsReadOnly { get; init; }
    public string? Search { get; init; }
    public TaskListResult Tasks { get; init; } = new();
}
