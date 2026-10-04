using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Security;
using Planora.Application.Common.Projects;
using Planora.Application.Common.Security;
using Planora.Application.Common.VModel;
using Planora.Domain.Enums;

namespace Planora.Web.Controllers;

public abstract class VModelArtifactControllerBase : Controller
{
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _projectAccessService;

    protected VModelArtifactControllerBase(
        IProjectService projectService,
        IProjectAccessService projectAccessService)
    {
        _projectService = projectService;
        _projectAccessService = projectAccessService;
    }

    protected async Task<(ArtifactScope? Scope, IActionResult? Error)> GetScopeAsync(
        int projectId, bool mutation, CancellationToken cancellationToken)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(userId))
            return (null, Challenge());
        if (!SystemRoles.All.Any(User.IsInRole)) return (null, Forbid());
        bool isAdmin = User.IsInRole(SystemRoles.Admin);
        ProjectSummary? project = await _projectService.GetAccessibleProjectByIdAsync(
            projectId, userId, isAdmin, cancellationToken);
        if (project is null)
        {
            bool exists = await _projectService.ProjectExistsAsync(projectId, cancellationToken);
            return (null, exists ? Forbid() : NotFound());
        }
        if (project.Methodology != ProjectMethodology.VModel) return (null, NotFound());
        ProjectMemberRole? projectRole = isAdmin ? null :
            await _projectAccessService.GetProjectRoleAsync(projectId, userId, cancellationToken);
        var scope = new ArtifactScope(project, userId, projectRole);
        if (mutation && scope.IsReadOnly)
            return (null, StatusCode(StatusCodes.Status409Conflict,
                "Archived projects are read-only."));
        return (scope, null);
    }

    protected bool CanManage(ArtifactScope scope) => User.IsInRole(SystemRoles.Admin) ||
        User.IsInRole(SystemRoles.ProjectManager) &&
        scope.ProjectRole == ProjectMemberRole.ProjectManager;

    protected bool CanCreateImplementation(ArtifactScope scope) => CanManage(scope) ||
        User.IsInRole(SystemRoles.Developer) &&
        scope.ProjectRole == ProjectMemberRole.Developer;

    protected bool CanManageTesting(ArtifactScope scope) => User.IsInRole(SystemRoles.Admin) ||
        User.IsInRole(SystemRoles.QaTester) && scope.ProjectRole == ProjectMemberRole.QaTester;

    protected bool CanEditImplementation(ArtifactScope scope, string creatorUserId) =>
        CanManage(scope) || User.IsInRole(SystemRoles.Developer) &&
        scope.ProjectRole == ProjectMemberRole.Developer && scope.UserId == creatorUserId;

    protected IActionResult Failure(ArtifactOperationResult result) => result.Failure switch
    {
        ArtifactOperationFailure.NotFound => NotFound(result.Message),
        ArtifactOperationFailure.Forbidden => StatusCode(StatusCodes.Status403Forbidden, result.Message),
        ArtifactOperationFailure.ReadOnly or ArtifactOperationFailure.Conflict =>
            StatusCode(StatusCodes.Status409Conflict, result.Message),
        _ => BadRequest(result.Message)
    };

    protected sealed record ArtifactScope(
        ProjectSummary Project, string UserId, ProjectMemberRole? ProjectRole)
    {
        public bool IsReadOnly => Project.Status == ProjectStatus.Archived;
    }
}
