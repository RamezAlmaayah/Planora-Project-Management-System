using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Planora.Application.Abstractions.Security;
using Planora.Application.Common.Security;

namespace Planora.Web.Authorization;

public sealed class ProjectMemberAuthorizationHandler
    : AuthorizationHandler<ProjectMemberRequirement, int>
{
    private readonly IProjectAccessService _projectAccessService;

    public ProjectMemberAuthorizationHandler(
        IProjectAccessService projectAccessService)
    {
        _projectAccessService = projectAccessService;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ProjectMemberRequirement requirement,
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

        var isMember =
            await _projectAccessService.IsProjectMemberAsync(
                projectId,
                userId);

        if (isMember)
        {
            context.Succeed(requirement);
        }
    }
}