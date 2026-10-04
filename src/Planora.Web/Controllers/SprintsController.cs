using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Scrum;
using Planora.Application.Abstractions.Security;
using Planora.Application.Common.Projects;
using Planora.Application.Common.Scrum.Sprints;
using Planora.Application.Common.Security;
using Planora.Domain.Enums;
using Planora.Web.ViewModels.Sprints;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class SprintsController : Controller
{
    private readonly ISprintService _sprintService;
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _projectAccessService;

    public SprintsController(
        ISprintService sprintService,
        IProjectService projectService,
        IProjectAccessService projectAccessService)
    {
        _sprintService = sprintService;
        _projectService = projectService;
        _projectAccessService = projectAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int projectId,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        ProjectSummary? project =
            await GetProjectAsync(
                projectId,
                userId,
                cancellationToken);

        if (project is null)
            return await NotFoundOrForbidden(
                projectId,
                cancellationToken);

        if (project.Methodology != ProjectMethodology.Scrum)
            return BadRequest("Sprints are available only for Scrum projects.");

        var sprints =
            await _sprintService
                .GetProjectSprintsAsync(
                    projectId,
                    cancellationToken);

        var model = new SprintIndexViewModel
        {
            ProjectId = project.Id,
            ProjectName = project.Name,

            IsArchived = project.Status == ProjectStatus.Archived,

            CanManage =
                await CanManageAsync(
                    projectId,
                    userId,
                    cancellationToken),

            Sprints = sprints
                .Select(x => new SprintListItemViewModel
                {
                    Id = x.Id,
                    Name = x.Name,
                    Goal = x.Goal,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate,
                    Status = x.Status,
                    BacklogItemCount = x.BacklogItemCount,
                    TaskCount = x.TaskCount
                })
                .ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(
        int projectId,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (!await CanManageAsync(
                projectId,
                userId,
                cancellationToken))
            return Forbid();

        ProjectSummary? project =
            await GetProjectAsync(
                projectId,
                userId,
                cancellationToken);

        if (project is null)
            return NotFound();

        if (project.Methodology != ProjectMethodology.Scrum)
            return BadRequest();

        if (project.Status == ProjectStatus.Archived)
            return RedirectToAction(
                nameof(Index),
                new { projectId });

        return View(new SprintFormViewModel
        {
            ProjectId = projectId,
            ProjectName = project.Name,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(14)
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        SprintFormViewModel model,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (!await CanManageAsync(
                model.ProjectId,
                userId,
                cancellationToken))
            return Forbid();

        ProjectSummary? project =
            await GetProjectAsync(
                model.ProjectId,
                userId,
                cancellationToken);

        if (project is null)
            return NotFound();

        model.ProjectName = project.Name;

        if (model.EndDate.Date <= model.StartDate.Date)
        {
            ModelState.AddModelError(
                nameof(model.EndDate),
                "End date must be later than start date.");
        }

        if (!ModelState.IsValid)
            return View(model);

        SprintOperationResult result =
            await _sprintService.CreateAsync(
                new CreateSprintRequest
                {
                    ProjectId = model.ProjectId,
                    Name = model.Name,
                    Goal = model.Goal,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    CreatedByUserId = userId
                },
                cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "Unable to create sprint.");

            return View(model);
        }

        TempData["SuccessMessage"] =
            "Sprint created successfully.";

        return RedirectToAction(
            nameof(Index),
            new { projectId = model.ProjectId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        int projectId,
        int id,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (!await CanManageAsync(
                projectId,
                userId,
                cancellationToken))
            return Forbid();

        SprintSummary? sprint =
            await _sprintService.GetByIdAsync(
                projectId,
                id,
                cancellationToken);

        if (sprint is null)
            return NotFound();

        if (sprint.Status == SprintStatus.Completed)
        {
            TempData["ErrorMessage"] =
                "Completed sprints are read-only.";

            return RedirectToAction(
                nameof(Index),
                new { projectId });
        }

        ProjectSummary? project =
            await GetProjectAsync(
                projectId,
                userId,
                cancellationToken);

        return View(new SprintFormViewModel
        {
            Id = sprint.Id,
            ProjectId = projectId,
            ProjectName = project?.Name ?? string.Empty,
            Name = sprint.Name,
            Goal = sprint.Goal,
            StartDate = sprint.StartDate,
            EndDate = sprint.EndDate
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(
        int projectId,
        int id,
        SprintFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.Id != id ||
            model.ProjectId != projectId)
            return BadRequest();

        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (!await CanManageAsync(
                projectId,
                userId,
                cancellationToken))
            return Forbid();

        if (model.EndDate.Date <= model.StartDate.Date)
        {
            ModelState.AddModelError(
                nameof(model.EndDate),
                "End date must be later than start date.");
        }

        if (!ModelState.IsValid)
            return View(model);

        SprintOperationResult result =
            await _sprintService.UpdateAsync(
                new UpdateSprintRequest
                {
                    Id = id,
                    ProjectId = projectId,
                    Name = model.Name,
                    Goal = model.Goal,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    UpdatedByUserId = userId
                },
                cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ?? "Unable to update sprint.");

            return View(model);
        }

        TempData["SuccessMessage"] =
            "Sprint updated successfully.";

        return RedirectToAction(
            nameof(Index),
            new { projectId });
    }

    [HttpPost]
    public async Task<IActionResult> Start(
        int projectId,
        int id,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (!await CanManageAsync(
                projectId,
                userId,
                cancellationToken))
            return Forbid();

        SprintOperationResult result =
            await _sprintService.StartAsync(
                projectId,
                id,
                userId,
                cancellationToken);

        TempData[result.Succeeded
            ? "SuccessMessage"
            : "ErrorMessage"] =
            result.Succeeded
                ? "Sprint started successfully."
                : result.Error;

        return RedirectToAction(
            nameof(Index),
            new { projectId });
    }

    [HttpPost]
    public async Task<IActionResult> Complete(
        int projectId,
        int id,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (!await CanManageAsync(
                projectId,
                userId,
                cancellationToken))
            return Forbid();

        SprintOperationResult result =
            await _sprintService.CompleteAsync(
                projectId,
                id,
                userId,
                cancellationToken);

        TempData[result.Succeeded
            ? "SuccessMessage"
            : "ErrorMessage"] =
            result.Succeeded
                ? "Sprint completed successfully."
                : result.Error;

        return RedirectToAction(
            nameof(Index),
            new { projectId });
    }

    [HttpGet]
    public async Task<IActionResult> ManageBacklog(
        int projectId,
        int id,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (!await CanManageAsync(
                projectId,
                userId,
                cancellationToken))
            return Forbid();

        ProjectSummary? project =
            await GetProjectAsync(
                projectId,
                userId,
                cancellationToken);

        if (project is null)
            return NotFound();

        SprintSummary? sprint =
            await _sprintService.GetByIdAsync(
                projectId,
                id,
                cancellationToken);

        if (sprint is null)
            return NotFound();

        var model = new SprintPlanningViewModel
        {
            ProjectId = projectId,
            ProjectName = project.Name,
            SprintId = sprint.Id,
            SprintName = sprint.Name,
            SprintStatus = sprint.Status,
            IsArchived = project.Status == ProjectStatus.Archived,

            ReadyItems =
                await _sprintService.GetReadyBacklogAsync(
                    projectId,
                    cancellationToken),

            SprintItems =
                await _sprintService.GetSprintBacklogAsync(
                    projectId,
                    id,
                    cancellationToken)
        };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> AddBacklogItem(
        int projectId,
        int id,
        int backlogItemId,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (!await CanManageAsync(
                projectId,
                userId,
                cancellationToken))
            return Forbid();

        SprintOperationResult result =
            await _sprintService.AddBacklogItemAsync(
                projectId,
                id,
                backlogItemId,
                userId,
                cancellationToken);

        TempData[result.Succeeded
            ? "SuccessMessage"
            : "ErrorMessage"] =
            result.Succeeded
                ? "Backlog item added to sprint."
                : result.Error;

        return RedirectToAction(
            nameof(ManageBacklog),
            new { projectId, id });
    }

    [HttpPost]
    public async Task<IActionResult> RemoveBacklogItem(
        int projectId,
        int id,
        int backlogItemId,
        CancellationToken cancellationToken)
    {
        string? userId = GetUserId();

        if (string.IsNullOrWhiteSpace(userId))
            return Challenge();

        if (!await CanManageAsync(
                projectId,
                userId,
                cancellationToken))
            return Forbid();

        SprintOperationResult result =
            await _sprintService.RemoveBacklogItemAsync(
                projectId,
                id,
                backlogItemId,
                userId,
                cancellationToken);

        TempData[result.Succeeded
            ? "SuccessMessage"
            : "ErrorMessage"] =
            result.Succeeded
                ? "Backlog item removed from sprint."
                : result.Error;

        return RedirectToAction(
            nameof(ManageBacklog),
            new { projectId, id });
    }
    private async Task<bool> CanManageAsync(
        int projectId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole(SystemRoles.Admin))
            return true;

        ProjectMemberRole? role =
            await _projectAccessService.GetProjectRoleAsync(
                projectId,
                userId,
                cancellationToken);

        if (User.IsInRole(SystemRoles.ProjectManager))
            return role == ProjectMemberRole.ProjectManager;

        if (User.IsInRole(SystemRoles.ScrumMaster))
            return role == ProjectMemberRole.ScrumMaster;

        return false;
    }

    private async Task<ProjectSummary?> GetProjectAsync(
        int projectId,
        string userId,
        CancellationToken cancellationToken)
    {
        return await _projectService
            .GetAccessibleProjectByIdAsync(
                projectId,
                userId,
                User.IsInRole(SystemRoles.Admin),
                cancellationToken);
    }

    private async Task<IActionResult> NotFoundOrForbidden(
        int projectId,
        CancellationToken cancellationToken)
    {
        bool exists =
            await _projectService.ProjectExistsAsync(
                projectId,
                cancellationToken);

        return exists ? Forbid() : NotFound();
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier);
    }
}

