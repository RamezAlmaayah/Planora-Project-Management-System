using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Security;
using Planora.Application.Common.Projects;
using Planora.Application.Common.Projects.Members;
using Planora.Application.Common.Security;
using Planora.Domain.Enums;
using Planora.Web.ViewModels.ProjectMembers;

namespace Planora.Web.Controllers;

[Authorize(
    Policy =
        AuthorizationPolicies.CanManageProjects)]
public sealed class ProjectMembersController : Controller
{
    private readonly IProjectMemberService _projectMemberService;
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _projectAccessService;
    private readonly ILogger<ProjectMembersController> _logger;

    public ProjectMembersController(
        IProjectMemberService projectMemberService,
        IProjectService projectService,
        IProjectAccessService projectAccessService,
        ILogger<ProjectMembersController> logger)
    {
        _projectMemberService = projectMemberService;
        _projectService = projectService;
        _projectAccessService = projectAccessService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Manage(
        int projectId,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(
                currentUserId))
        {
            return Challenge();
        }

        bool canManage =
            await CanManageProjectAsync(
                projectId,
                currentUserId,
                cancellationToken);

        if (!canManage)
        {
            return await ProjectAccessFailureAsync(
                projectId,
                cancellationToken);
        }

        bool isAdmin =
            User.IsInRole(
                SystemRoles.Admin);

        ProjectSummary? project =
            await _projectService
                .GetAccessibleProjectByIdAsync(
                    projectId,
                    currentUserId,
                    isAdmin,
                    cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        IReadOnlyList<ProjectMemberSummary> members =
            await _projectMemberService
                .GetMembersAsync(
                    projectId,
                    cancellationToken);

        IReadOnlyList<AvailableProjectUser>
            availableUsers =
                project.Status ==
                    ProjectStatus.Archived
                    ? Array.Empty<AvailableProjectUser>()
                    : await _projectMemberService
                        .GetAvailableUsersAsync(
                            projectId,
                            cancellationToken);

        var model =
            new ManageProjectMembersViewModel
            {
                ProjectId =
                    project.Id,

                ProjectName =
                    project.Name,

                Methodology =
                    project.Methodology,

                Status =
                    project.Status,

                Members =
                    members
                        .Select(member =>
                            new ProjectMemberItemViewModel
                            {
                                MembershipId =
                                    member.Id,

                                UserId =
                                    member.UserId,

                                FullName =
                                    member.FullName,

                                Email =
                                    member.Email,

                                GlobalRole =
                                    member.GlobalRole,

                                IsDisabled =
                                    member.IsDisabled,

                                Role =
                                    member.Role,

                                JoinedAt =
                                    member.JoinedAt
                            })
                        .ToList(),

                AvailableUsers =
                    availableUsers
                        .Select(user =>
                            new AvailableProjectUserViewModel
                            {
                                UserId =
                                    user.UserId,

                                FullName =
                                    user.FullName,

                                Email =
                                    user.Email,

                                EligibleRoles =
                                    user.EligibleProjectRoles
                            })
                        .ToList()
            };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Add(
        int projectId,
        string userId,
        ProjectMemberRole role,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(
                currentUserId))
        {
            return Challenge();
        }

        bool canManage =
            await CanManageProjectAsync(
                projectId,
                currentUserId,
                cancellationToken);

        if (!canManage)
        {
            return await ProjectAccessFailureAsync(
                projectId,
                cancellationToken);
        }

        var request =
            new AddProjectMemberRequest
            {
                ProjectId =
                    projectId,

                UserId =
                    userId,

                Role =
                    role,

                ActorUserId =
                    currentUserId
            };

        ProjectMemberOperationResult result =
            await _projectMemberService
                .AddMemberAsync(
                    request,
                    cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] =
                result.ErrorMessage
                ?? "Unable to add project member.";

            return RedirectToAction(
                nameof(Manage),
                new { projectId });
        }

        _logger.LogInformation(
            "User {AddedUserId} added to project {ProjectId} by user {CurrentUserId}.",
            userId,
            projectId,
            currentUserId);

        TempData["SuccessMessage"] =
            "Project member was added successfully.";

        return RedirectToAction(
            nameof(Manage),
            new { projectId });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateRole(
        int projectId,
        int membershipId,
        ProjectMemberRole role,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(
                currentUserId))
        {
            return Challenge();
        }

        bool canManage =
            await CanManageProjectAsync(
                projectId,
                currentUserId,
                cancellationToken);

        if (!canManage)
        {
            return await ProjectAccessFailureAsync(
                projectId,
                cancellationToken);
        }

        var request =
            new UpdateProjectMemberRoleRequest
            {
                ProjectId = projectId,

                MembershipId =
                    membershipId,

                Role =
                    role,

                ActorUserId =
                    currentUserId
            };

        ProjectMemberOperationResult result =
            await _projectMemberService
                .UpdateMemberRoleAsync(
                    request,
                    cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] =
                result.ErrorMessage
                ?? "Unable to update project member role.";

            return RedirectToAction(
                nameof(Manage),
                new { projectId });
        }

        _logger.LogInformation(
            "Membership {MembershipId} role updated in project {ProjectId} by user {CurrentUserId}.",
            membershipId,
            projectId,
            currentUserId);

        TempData["SuccessMessage"] =
            "Project member role was updated successfully.";

        return RedirectToAction(
            nameof(Manage),
            new { projectId });
    }

    [HttpPost]
    public async Task<IActionResult> Remove(
        int projectId,
        int membershipId,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(
                currentUserId))
        {
            return Challenge();
        }

        bool canManage =
            await CanManageProjectAsync(
                projectId,
                currentUserId,
                cancellationToken);

        if (!canManage)
        {
            return await ProjectAccessFailureAsync(
                projectId,
                cancellationToken);
        }

        ProjectMemberOperationResult result =
            await _projectMemberService
                .RemoveMemberAsync(
                    projectId,
                    membershipId,
                    currentUserId,
                    cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] =
                result.ErrorMessage
                ?? "Unable to remove project member.";

            return RedirectToAction(
                nameof(Manage),
                new { projectId });
        }

        _logger.LogInformation(
            "Membership {MembershipId} removed from project {ProjectId} by user {CurrentUserId}.",
            membershipId,
            projectId,
            currentUserId);

        TempData["SuccessMessage"] =
            "Project member was removed successfully.";

        return RedirectToAction(
            nameof(Manage),
            new { projectId });
    }

    private async Task<bool>
        CanManageProjectAsync(
            int projectId,
            string userId,
            CancellationToken cancellationToken)
    {
        if (User.IsInRole(
                SystemRoles.Admin))
        {
            return true;
        }

        if (!User.IsInRole(
                SystemRoles.ProjectManager))
        {
            return false;
        }

        ProjectMemberRole? projectRole =
            await _projectAccessService
                .GetProjectRoleAsync(
                    projectId,
                    userId,
                    cancellationToken);

        return projectRole ==
            ProjectMemberRole.ProjectManager;
    }

    private async Task<IActionResult>
        ProjectAccessFailureAsync(
            int projectId,
            CancellationToken cancellationToken)
    {
        bool projectExists =
            await _projectService
                .ProjectExistsAsync(
                    projectId,
                    cancellationToken);

        return projectExists
            ? Forbid()
            : NotFound();
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier);
    }
}
