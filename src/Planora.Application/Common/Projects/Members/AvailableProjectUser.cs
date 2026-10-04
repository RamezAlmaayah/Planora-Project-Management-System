using Planora.Domain.Enums;

namespace Planora.Application.Common.Projects.Members;

public sealed class AvailableProjectUser
{
    public string UserId { get; set; } =
        string.Empty;

    public string FullName { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public IReadOnlyList<ProjectMemberRole>
        EligibleProjectRoles
    { get; set; }
        = Array.Empty<ProjectMemberRole>();
}