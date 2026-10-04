using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Scrum;
using Planora.Application.Abstractions.Security;
using Planora.Application.Common.Projects;
using Planora.Application.Common.Scrum.Backlog;
using Planora.Application.Common.Security;
using Planora.Domain.Enums;
using Planora.Web.ViewModels.Backlog;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class BacklogController : Controller
{
    private readonly IBacklogService _backlogService;
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _projectAccessService;
    private readonly ILogger<BacklogController> _logger;

    public BacklogController(
        IBacklogService backlogService,
        IProjectService projectService,
        IProjectAccessService projectAccessService,
        ILogger<BacklogController> logger)
    {
        _backlogService = backlogService;
        _projectService = projectService;
        _projectAccessService = projectAccessService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int projectId,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        ProjectSummary? project =
            await GetAccessibleProjectAsync(
                projectId,
                currentUserId,
                cancellationToken);

        if (project is null)
        {
            return await ProjectNotFoundOrForbiddenAsync(
                projectId,
                cancellationToken);
        }

        if (project.Methodology !=
            ProjectMethodology.Scrum)
        {
            return BadRequest(
                "Product Backlog is available only for Scrum projects.");
        }

        IReadOnlyList<BacklogItemSummary> items =
            await _backlogService
                .GetProjectBacklogAsync(
                    projectId,
                    cancellationToken);

        bool canManage =
            await CanManageBacklogAsync(
                projectId,
                currentUserId,
                cancellationToken);

        var model =
            new BacklogIndexViewModel
            {
                ProjectId =
                    project.Id,

                ProjectName =
                    project.Name,

                CanManage =
                    canManage,

                Items =
                    items.Select(MapToListItem)
                        .ToList()
            };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Create(
        int projectId,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        if (!await CanManageBacklogAsync(
                projectId,
                currentUserId,
                cancellationToken))
        {
            return await ProjectNotFoundOrForbiddenAsync(
                projectId,
                cancellationToken);
        }

        ProjectSummary? project =
            await GetAccessibleProjectAsync(
                projectId,
                currentUserId,
                cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        if (project.Methodology !=
            ProjectMethodology.Scrum)
        {
            return BadRequest(
                "Backlog items can only be created for Scrum projects.");
        }

        if (project.Status ==
            ProjectStatus.Archived)
        {
            TempData["ErrorMessage"] =
                "Archived projects cannot be modified.";

            return RedirectToAction(
                nameof(Index),
                new { projectId });
        }

        var model =
            new BacklogFormViewModel
            {
                ProjectId =
                    project.Id,

                ProjectName =
                    project.Name,

                Priority =
                    PriorityLevel.Medium,

                Status =
                    BacklogItemStatus.Draft
            };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        BacklogFormViewModel model,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        if (!await CanManageBacklogAsync(
                model.ProjectId,
                currentUserId,
                cancellationToken))
        {
            return await ProjectNotFoundOrForbiddenAsync(
                model.ProjectId,
                cancellationToken);
        }

        ProjectSummary? project =
            await GetAccessibleProjectAsync(
                model.ProjectId,
                currentUserId,
                cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        model.ProjectName =
            project.Name;

        NormalizeModel(model);

        ValidateManualStatus(
            model.Status);

        ValidatePriority(
            model.Priority);

        if (project.Methodology !=
            ProjectMethodology.Scrum)
        {
            ModelState.AddModelError(
                string.Empty,
                "Backlog items can only be created for Scrum projects.");
        }

        if (project.Status ==
            ProjectStatus.Archived)
        {
            ModelState.AddModelError(
                string.Empty,
                "Archived projects cannot be modified.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request =
            new CreateBacklogItemRequest
            {
                ProjectId =
                    model.ProjectId,

                Title =
                    model.Title,

                Description =
                    model.Description,

                Priority =
                    model.Priority,

                Status =
                    model.Status,

                CreatedByUserId =
                    currentUserId
            };

        BacklogOperationResult result =
            await _backlogService
                .CreateAsync(
                    request,
                    cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ??
                "Unable to create backlog item.");

            return View(model);
        }

        _logger.LogInformation(
            "Backlog item {BacklogItemId} created in project {ProjectId} by user {UserId}.",
            result.Item?.Id,
            model.ProjectId,
            currentUserId);

        TempData["SuccessMessage"] =
            $"Backlog item '{result.Item?.Title}' was created successfully.";

        return RedirectToAction(
            nameof(Index),
            new
            {
                projectId =
                    model.ProjectId
            });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        int projectId,
        int id,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        if (!await CanManageBacklogAsync(
                projectId,
                currentUserId,
                cancellationToken))
        {
            return await ProjectNotFoundOrForbiddenAsync(
                projectId,
                cancellationToken);
        }

        ProjectSummary? project =
            await GetAccessibleProjectAsync(
                projectId,
                currentUserId,
                cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        BacklogItemSummary? item =
            await _backlogService
                .GetByIdAsync(
                    projectId,
                    id,
                    cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        if (project.Status ==
            ProjectStatus.Archived)
        {
            TempData["ErrorMessage"] =
                "Archived projects cannot be modified.";

            return RedirectToAction(
                nameof(Index),
                new { projectId });
        }

        if (item.Status ==
            BacklogItemStatus.InSprint
            || item.Status ==
                BacklogItemStatus.Completed)
        {
            TempData["ErrorMessage"] =
                "This backlog item's status is managed automatically.";

            return RedirectToAction(
                nameof(Index),
                new { projectId });
        }

        var model =
            new BacklogFormViewModel
            {
                Id =
                    item.Id,

                ProjectId =
                    project.Id,

                ProjectName =
                    project.Name,

                Title =
                    item.Title,

                Description =
                    item.Description,

                Priority =
                    item.Priority,

                Status =
                    item.Status,

                IsAssignedToSprint =
                    item.IsAssignedToSprint
            };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(
        int projectId,
        int id,
        BacklogFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (model.Id != id
            || model.ProjectId != projectId)
        {
            return BadRequest();
        }

        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        if (!await CanManageBacklogAsync(
                projectId,
                currentUserId,
                cancellationToken))
        {
            return await ProjectNotFoundOrForbiddenAsync(
                projectId,
                cancellationToken);
        }

        ProjectSummary? project =
            await GetAccessibleProjectAsync(
                projectId,
                currentUserId,
                cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        BacklogItemSummary? existingItem =
            await _backlogService
                .GetByIdAsync(
                    projectId,
                    id,
                    cancellationToken);

        if (existingItem is null)
        {
            return NotFound();
        }

        model.ProjectName =
            project.Name;

        NormalizeModel(model);

        ValidateManualStatus(
            model.Status);

        ValidatePriority(
            model.Priority);

        if (existingItem.Status ==
            BacklogItemStatus.InSprint
            || existingItem.Status ==
                BacklogItemStatus.Completed)
        {
            ModelState.AddModelError(
                string.Empty,
                "This backlog item's status is managed automatically.");
        }

        if (project.Status ==
            ProjectStatus.Archived)
        {
            ModelState.AddModelError(
                string.Empty,
                "Archived projects cannot be modified.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request =
            new UpdateBacklogItemRequest
            {
                Id =
                    id,

                ProjectId =
                    projectId,

                Title =
                    model.Title,

                Description =
                    model.Description,

                Priority =
                    model.Priority,

                Status =
                    model.Status,

                UpdatedByUserId =
                    currentUserId
            };

        BacklogOperationResult result =
            await _backlogService
                .UpdateAsync(
                    request,
                    cancellationToken);

        if (!result.Succeeded)
        {
            ModelState.AddModelError(
                string.Empty,
                result.Error ??
                "Unable to update backlog item.");

            return View(model);
        }

        _logger.LogInformation(
            "Backlog item {BacklogItemId} updated in project {ProjectId} by user {UserId}.",
            id,
            projectId,
            currentUserId);

        TempData["SuccessMessage"] =
            $"Backlog item '{result.Item?.Title}' was updated successfully.";

        return RedirectToAction(
            nameof(Index),
            new { projectId });
    }

    [HttpGet]
    public async Task<IActionResult> ConfirmDelete(
        int projectId,
        int id,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        if (!await CanManageBacklogAsync(
                projectId,
                currentUserId,
                cancellationToken))
        {
            return await ProjectNotFoundOrForbiddenAsync(
                projectId,
                cancellationToken);
        }

        ProjectSummary? project =
            await GetAccessibleProjectAsync(
                projectId,
                currentUserId,
                cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        BacklogItemSummary? item =
            await _backlogService
                .GetByIdAsync(
                    projectId,
                    id,
                    cancellationToken);

        if (item is null)
        {
            return NotFound();
        }

        var model =
            new BacklogDeleteViewModel
            {
                Id =
                    item.Id,

                ProjectId =
                    project.Id,

                ProjectName =
                    project.Name,

                Title =
                    item.Title,

                Priority =
                    item.Priority,

                Status =
                    item.Status,

                IsAssignedToSprint =
                    item.IsAssignedToSprint
            };

        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(
        int projectId,
        int id,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        if (!await CanManageBacklogAsync(
                projectId,
                currentUserId,
                cancellationToken))
        {
            return await ProjectNotFoundOrForbiddenAsync(
                projectId,
                cancellationToken);
        }

        BacklogOperationResult result =
            await _backlogService
                .DeleteAsync(
                    projectId,
                    id,
                    cancellationToken);

        if (!result.Succeeded)
        {
            TempData["ErrorMessage"] =
                result.Error ??
                "Unable to delete backlog item.";

            return RedirectToAction(
                nameof(Index),
                new { projectId });
        }

        _logger.LogInformation(
            "Backlog item {BacklogItemId} deleted from project {ProjectId} by user {UserId}.",
            id,
            projectId,
            currentUserId);

        TempData["SuccessMessage"] =
            "Backlog item was deleted successfully.";

        return RedirectToAction(
            nameof(Index),
            new { projectId });
    }

    private async Task<ProjectSummary?>
        GetAccessibleProjectAsync(
            int projectId,
            string userId,
            CancellationToken cancellationToken)
    {
        bool isAdmin =
            User.IsInRole(
                SystemRoles.Admin);

        return await _projectService
            .GetAccessibleProjectByIdAsync(
                projectId,
                userId,
                isAdmin,
                cancellationToken);
    }

    private async Task<bool>
        CanManageBacklogAsync(
            int projectId,
            string userId,
            CancellationToken cancellationToken)
    {
        if (User.IsInRole(
                SystemRoles.Admin))
        {
            return true;
        }

        ProjectMemberRole? projectRole =
            await _projectAccessService
                .GetProjectRoleAsync(
                    projectId,
                    userId,
                    cancellationToken);

        if (User.IsInRole(
                SystemRoles.ProjectManager))
        {
            return projectRole ==
                ProjectMemberRole.ProjectManager;
        }

        if (User.IsInRole(
                SystemRoles.ScrumMaster))
        {
            return projectRole ==
                ProjectMemberRole.ScrumMaster;
        }

        return false;
    }

    private async Task<IActionResult>
        ProjectNotFoundOrForbiddenAsync(
            int projectId,
            CancellationToken cancellationToken)
    {
        bool exists =
            await _projectService
                .ProjectExistsAsync(
                    projectId,
                    cancellationToken);

        return exists
            ? Forbid()
            : NotFound();
    }

    private void ValidateManualStatus(
        BacklogItemStatus status)
    {
        if (status !=
                BacklogItemStatus.Draft
            && status !=
                BacklogItemStatus.Ready)
        {
            ModelState.AddModelError(
                nameof(BacklogFormViewModel.Status),
                "Only Draft or Ready can be selected manually.");
        }
    }

    private void ValidatePriority(
        PriorityLevel priority)
    {
        if (!Enum.IsDefined(
                typeof(PriorityLevel),
                priority))
        {
            ModelState.AddModelError(
                nameof(BacklogFormViewModel.Priority),
                "Please select a valid priority.");
        }
    }

    private static void NormalizeModel(
        BacklogFormViewModel model)
    {
        model.Title =
            model.Title?.Trim()
            ?? string.Empty;

        model.Description =
            model.Description?.Trim()
            ?? string.Empty;
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirstValue(
            ClaimTypes.NameIdentifier);
    }

    private static BacklogItemListViewModel
        MapToListItem(
            BacklogItemSummary item)
    {
        return new BacklogItemListViewModel
        {
            Id =
                item.Id,

            Title =
                item.Title,

            Description =
                item.Description,

            Priority =
                item.Priority,

            Status =
                item.Status,

            CreatedAt =
                item.CreatedAt,

            UpdatedAt =
                item.UpdatedAt,

            IsAssignedToSprint =
                item.IsAssignedToSprint
        };
    }
}
