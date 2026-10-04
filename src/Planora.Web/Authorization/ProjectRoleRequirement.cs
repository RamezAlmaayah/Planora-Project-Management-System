using Microsoft.AspNetCore.Authorization;
using Planora.Domain.Enums;

namespace Planora.Web.Authorization;

public sealed class ProjectRoleRequirement
    : IAuthorizationRequirement
{
    public IReadOnlyCollection<ProjectMemberRole> AllowedRoles { get; }

    public ProjectRoleRequirement(
        params ProjectMemberRole[] allowedRoles)
    {
        AllowedRoles = allowedRoles;
    }
}