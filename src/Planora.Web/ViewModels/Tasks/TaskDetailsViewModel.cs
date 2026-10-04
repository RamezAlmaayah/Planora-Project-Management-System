using Planora.Application.Common.Tasks;

namespace Planora.Web.ViewModels.Tasks;

public sealed class TaskDetailsViewModel
{
    public string ProjectName { get; init; } = string.Empty;
    public string SprintName { get; init; } = string.Empty;
    public bool CanEdit { get; init; }
    public bool IsReadOnly { get; init; }
    public string CurrentUserId { get; init; } = string.Empty;
    public bool IsAdmin { get; init; }
    public bool CanUploadAttachment { get; init; }
    public bool CanDeleteAnyAttachment { get; init; }
    public bool CanDeleteOwnAttachment { get; init; }
    public bool CanReportIssue { get; init; }
    public TaskDetails Task { get; init; } = new();
}
