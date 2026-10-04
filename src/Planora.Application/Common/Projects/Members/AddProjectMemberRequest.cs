using Planora.Domain.Enums;

namespace Planora.Application.Common.Projects.Members;

public sealed class AddProjectMemberRequest
{
    public int ProjectId { get; set; }

    public string UserId { get; set; } =
        string.Empty;

    public ProjectMemberRole Role { get; set; }

    public string ActorUserId { get; set; } = string.Empty;
}
