using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Tasks;

public sealed class TaskStatusUpdateViewModel
{
    public int ProjectId { get; set; }
    public int SprintId { get; set; }
    public int TaskId { get; set; }
    public TaskItemStatus CurrentStatus { get; set; }
    public TaskItemStatus TargetStatus { get; set; }
}
