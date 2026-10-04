using Planora.Domain.Enums;

namespace Planora.Application.Common.Projects.Members;

public sealed class UpdateProjectMemberRoleRequest
{
    public int ProjectId { get; set; }

    public int MembershipId { get; set; }

    public ProjectMemberRole Role { get; set; }

    public string ActorUserId { get; set; } = string.Empty;
}
