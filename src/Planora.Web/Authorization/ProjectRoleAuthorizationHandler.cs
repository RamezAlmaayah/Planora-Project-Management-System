using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Planora.Application.Abstractions.Security;
using Planora.Application.Common.Security;

namespace Planora.Web.Authorization;

public sealed class ProjectRoleAuthorizationHandler
    : AuthorizationHandler<ProjectRoleRequirement, int>
{
    private readonly IProjectAccessService _projectAccessService;

    public ProjectRoleAuthorizationHandler(
        IProjectAccessService projectAccessService)
    {
        _projectAccessService = projectAccessService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProjectRoleRequirement requirement,
        int projectId)
    {
        if (context.User.IsInRole(SystemRoles.Admin))
        {
            context.Succeed(requirement);
            return;
        }

        var userId = context.User.FindFirstValue(
            ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        var projectRole =
            await _projectAccessService.GetProjectRoleAsync(
                projectId,
                userId);

        if (projectRole.HasValue
            && requirement.AllowedRoles.Contains(projectRole.Value))
        {
            context.Succeed(requirement);
        }
    }
}