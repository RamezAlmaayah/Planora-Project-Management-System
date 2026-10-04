using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.ProjectMembers;

public sealed class ProjectMemberItemViewModel
{
    public int MembershipId { get; set; }

    public string UserId { get; set; } =
        string.Empty;

    public string FullName { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public string GlobalRole { get; set; } =
        string.Empty;

    public bool IsDisabled { get; set; }

    public ProjectMemberRole Role { get; set; }

    public DateTime JoinedAt { get; set; }
}
