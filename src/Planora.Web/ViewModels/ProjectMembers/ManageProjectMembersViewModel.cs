using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.ProjectMembers;

public sealed class ManageProjectMembersViewModel
{
    public int ProjectId { get; set; }

    public string ProjectName { get; set; } =
        string.Empty;

    public ProjectMethodology Methodology { get; set; }

    public ProjectStatus Status { get; set; }

    public IReadOnlyList<ProjectMemberItemViewModel>
        Members
    { get; set; }
        = Array.Empty<ProjectMemberItemViewModel>();

    public IReadOnlyList<AvailableProjectUserViewModel>
        AvailableUsers
    { get; set; }
        = Array.Empty<AvailableProjectUserViewModel>();

    public bool IsArchived =>
        Status == ProjectStatus.Archived;
}