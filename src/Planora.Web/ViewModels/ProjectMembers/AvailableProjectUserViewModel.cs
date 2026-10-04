using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.ProjectMembers;

public sealed class AvailableProjectUserViewModel
{
    public string UserId { get; set; } =
        string.Empty;

    public string FullName { get; set; } =
        string.Empty;

    public string Email { get; set; } =
        string.Empty;

    public IReadOnlyList<ProjectMemberRole>
        EligibleRoles
    { get; set; }
        = Array.Empty<ProjectMemberRole>();
}