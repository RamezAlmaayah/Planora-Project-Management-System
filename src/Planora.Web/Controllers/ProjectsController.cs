using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Security;
using Planora.Application.Common.Projects;
using Planora.Application.Common.Security;
using Planora.Domain.Enums;
using Planora.Web.ViewModels.Projects;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class ProjectsController : Controller
{
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _projectAccessService;
    private readonly IClock _clock;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        IProjectService projectService,
        IProjectAccessService projectAccessService,
        IClock clock,
        ILogger<ProjectsController> logger)
    {
        _projectService = projectService;
        _projectAccessService = projectAccessService;
        _clock = clock;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        CancellationToken cancellationToken,
        ProjectStatus? status = null)
    {
        string? userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        bool isAdmin =
            User.IsInRole(
                SystemRoles.Admin);

        IReadOnlyList<ProjectSummary> projects =
            await _projectService
                .GetAccessibleProjectsAsync(
                    userId,
                    isAdmin,
                    cancellationToken);

        IEnumerable<ProjectSummary> filteredProjects =
            projects;

        if (status.HasValue)
        {
            filteredProjects = filteredProjects.Where(project => project.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            string normalizedSearch =
                search.Trim();

            filteredProjects =
                filteredProjects.Where(project =>
                    project.Name.Contains(
                        normalizedSearch,
                        StringComparison.OrdinalIgnoreCase)
                    ||
                    project.Description.Contains(
                        normalizedSearch,
                        StringComparison.OrdinalIgnoreCase));
        }

        var model =
            new ProjectIndexViewModel
            {
                Search =
                    search?.Trim(),

                Status = status,

                Projects =
                    filteredProjects
                        .Select(MapToListItem)
                        .ToList()
            };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int id,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        bool isAdmin =
            User.IsInRole(
                SystemRoles.Admin);

        ProjectSummary? project =
            await _projectService
                .GetAccessibleProjectByIdAsync(
                    id,
                    currentUserId,
                    isAdmin,
                    cancellationToken);

        if (project is null)
        {
            bool projectExists =
                await _projectService
                    .ProjectExistsAsync(
                        id,
                        cancellationToken);

            if (projectExists)
            {
                return Forbid();
            }

            return NotFound();
        }

        bool canManage =
            await CanManageProjectAsync(
                id,
                currentUserId,
                cancellationToken);

        var model =
            new ProjectDetailsViewModel
            {
                Id =
                    project.Id,

                Name =
                    project.Name,

                Description =
                    project.Description,

                Methodology =
                    project.Methodology,

                Status =
                    project.Status,

                StartDate =
                    project.StartDate,

                EndDate =
                    project.EndDate,

                MemberCount =
                    project.MemberCount,

                CreatedAt =
                    project.CreatedAt,

                CanManage =
                    canManage
            };

        return View(model);
    }

    [HttpGet]
    [Authorize(
        Policy =
            AuthorizationPolicies.CanManageProjects)]
    public IActionResult Create()
    {
        var model =
            new ProjectFormViewModel
            {
                StartDate =
                    _clock.UtcNow.Date,

                Status =
                    ProjectStatus.Planning,

                Methodology =
                    ProjectMethodology.Scrum
            };

        return View(model);
    }

    [HttpPost]
    [Authorize(
        Policy =
            AuthorizationPolicies.CanManageProjects)]
    public async Task<IActionResult> Create(
        ProjectFormViewModel model,
        CancellationToken cancellationToken)
    {
        DateTime today =
            _clock.UtcNow.Date;

        NormalizeModel(model);

        if (model.StartDate.Date < today)
        {
            ModelState.AddModelError(
                nameof(model.StartDate),
                "Start date cannot be before today.");
        }

        ValidateDates(model);

        ValidateMethodology(
            model.Methodology);

        if (!string.IsNullOrWhiteSpace(model.Name))
        {
            bool duplicateName =
                await _projectService
                    .ProjectNameExistsAsync(
                        model.Name,
                        cancellationToken:
                            cancellationToken);

            if (duplicateName)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "A project with this name already exists.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.Status =
                ProjectStatus.Planning;

            return View(model);
        }

        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        bool creatorIsProjectManager =
            User.IsInRole(
                SystemRoles.ProjectManager);

        var request =
            new CreateProjectRequest
            {
                Name =
                    model.Name,

                Description =
                    model.Description,

                Methodology =
                    model.Methodology,

                StartDate =
                    model.StartDate.Date,

                EndDate =
                    model.EndDate?.Date,

                CreatedByUserId =
                    currentUserId,

                AddCreatorAsProjectManager =
                    creatorIsProjectManager
            };

        ProjectSummary project =
            await _projectService
                .CreateProjectAsync(
                    request,
                    cancellationToken);

        _logger.LogInformation(
            "Project {ProjectId} created by user {UserId}.",
            project.Id,
            currentUserId);

        TempData["SuccessMessage"] =
            $"Project '{project.Name}' was created successfully.";

        return RedirectToAction(
            nameof(Index));
    }

    [HttpGet]
    [Authorize(
        Policy =
            AuthorizationPolicies.CanManageProjects)]
    public async Task<IActionResult> Edit(
        int id,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        bool canManage =
            await CanManageProjectAsync(
                id,
                currentUserId,
                cancellationToken);

        if (!canManage)
        {
            bool projectExists =
                await _projectService
                    .ProjectExistsAsync(
                        id,
                        cancellationToken);

            if (projectExists)
            {
                return Forbid();
            }

            return NotFound();
        }

        bool isAdmin =
            User.IsInRole(
                SystemRoles.Admin);

        ProjectSummary? project =
            await _projectService
                .GetAccessibleProjectByIdAsync(
                    id,
                    currentUserId,
                    isAdmin,
                    cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        if (project.Status ==
            ProjectStatus.Archived)
        {
            TempData["ErrorMessage"] =
                "Archived projects cannot be edited.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        var model =
            new ProjectFormViewModel
            {
                Id =
                    project.Id,

                Name =
                    project.Name,

                Description =
                    project.Description,

                Methodology =
                    project.Methodology,

                Status =
                    project.Status,

                StartDate =
                    project.StartDate,

                EndDate =
                    project.EndDate
            };

        return View(model);
    }

    [HttpPost]
    [Authorize(
        Policy =
            AuthorizationPolicies.CanManageProjects)]
    public async Task<IActionResult> Edit(
        int id,
        ProjectFormViewModel model,
        CancellationToken cancellationToken)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        bool canManage =
            await CanManageProjectAsync(
                id,
                currentUserId,
                cancellationToken);

        if (!canManage)
        {
            bool projectExists =
                await _projectService
                    .ProjectExistsAsync(
                        id,
                        cancellationToken);

            if (projectExists)
            {
                return Forbid();
            }

            return NotFound();
        }

        bool isAdmin =
            User.IsInRole(
                SystemRoles.Admin);

        ProjectSummary? existingProject =
            await _projectService
                .GetAccessibleProjectByIdAsync(
                    id,
                    currentUserId,
                    isAdmin,
                    cancellationToken);

        if (existingProject is null)
        {
            return NotFound();
        }

        if (existingProject.Status ==
            ProjectStatus.Archived)
        {
            TempData["ErrorMessage"] =
                "Archived projects cannot be edited.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        NormalizeModel(model);

        ValidateDates(model);

        ValidateMethodology(
            model.Methodology);

        if (!Enum.IsDefined(
                typeof(ProjectStatus),
                model.Status))
        {
            ModelState.AddModelError(
                nameof(model.Status),
                "Please select a valid project status.");
        }

        if (model.Status ==
            ProjectStatus.Archived)
        {
            ModelState.AddModelError(
                nameof(model.Status),
                "Use the Archive action to archive a project.");
        }

        if (!string.IsNullOrWhiteSpace(model.Name))
        {
            bool duplicateName =
                await _projectService
                    .ProjectNameExistsAsync(
                        model.Name,
                        excludedProjectId:
                            id,
                        cancellationToken:
                            cancellationToken);

            if (duplicateName)
            {
                ModelState.AddModelError(
                    nameof(model.Name),
                    "A project with this name already exists.");
            }
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request =
            new UpdateProjectRequest
            {
                Id =
                    id,

                Name =
                    model.Name,

                Description =
                    model.Description,

                Methodology =
                    model.Methodology,

                Status =
                    model.Status,

                StartDate =
                    model.StartDate.Date,

                EndDate =
                    model.EndDate?.Date,

                UpdatedByUserId =
                    currentUserId
            };

        ProjectSummary? updatedProject =
            await _projectService
                .UpdateProjectAsync(
                    request,
                    cancellationToken);

        if (updatedProject is null)
        {
            return NotFound();
        }

        _logger.LogInformation(
            "Project {ProjectId} updated by user {UserId}.",
            id,
            currentUserId);

        TempData["SuccessMessage"] =
            $"Project '{updatedProject.Name}' was updated successfully.";

        return RedirectToAction(
            nameof(Details),
            new { id });
    }

    // =====================================================
    // CONFIRM ARCHIVE
    // =====================================================

    [HttpGet]
    [Authorize(
        Policy =
            AuthorizationPolicies.CanManageProjects)]
    public async Task<IActionResult> ConfirmArchive(
        int id,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        bool canManage =
            await CanManageProjectAsync(
                id,
                currentUserId,
                cancellationToken);

        if (!canManage)
        {
            bool projectExists =
                await _projectService
                    .ProjectExistsAsync(
                        id,
                        cancellationToken);

            if (projectExists)
            {
                return Forbid();
            }

            return NotFound();
        }

        bool isAdmin =
            User.IsInRole(
                SystemRoles.Admin);

        ProjectSummary? project =
            await _projectService
                .GetAccessibleProjectByIdAsync(
                    id,
                    currentUserId,
                    isAdmin,
                    cancellationToken);

        if (project is null)
        {
            return NotFound();
        }

        if (project.Status ==
            ProjectStatus.Archived)
        {
            TempData["ErrorMessage"] =
                "This project is already archived.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        var model =
            new ProjectDetailsViewModel
            {
                Id =
                    project.Id,

                Name =
                    project.Name,

                Description =
                    project.Description,

                Methodology =
                    project.Methodology,

                Status =
                    project.Status,

                StartDate =
                    project.StartDate,

                EndDate =
                    project.EndDate,

                MemberCount =
                    project.MemberCount,

                CreatedAt =
                    project.CreatedAt,

                CanManage =
                    true
            };

        return View(model);
    }

    [HttpPost]
    [Authorize(
        Policy =
            AuthorizationPolicies.CanManageProjects)]
    public async Task<IActionResult> Archive(
        int id,
        CancellationToken cancellationToken)
    {
        string? currentUserId =
            GetCurrentUserId();

        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return Challenge();
        }

        bool canManage =
            await CanManageProjectAsync(
                id,
                currentUserId,
                cancellationToken);

        if (!canManage)
        {
            bool projectExists =
                await _projectService
                    .ProjectExistsAsync(
                        id,
                        cancellationToken);

            if (projectExists)
            {
                return Forbid();
            }

            return NotFound();
        }

        bool archived =
            await _projectService
                .ArchiveProjectAsync(
                    id,
                    currentUserId,
                    cancellationToken);

        if (!archived)
        {
            return NotFound();
        }

        _logger.LogInformation(
            "Project {ProjectId} archived by user {UserId}.",
            id,
            currentUserId);

        TempData["SuccessMessage"] =
            "Project was archived successfully.";

        return RedirectToAction(
            nameof(Details),
            new { id });
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

    private void ValidateMethodology(
        ProjectMethodology methodology)
    {
        if (!Enum.IsDefined(
                typeof(ProjectMethodology),
                methodology))
        {
            ModelState.AddModelError(
                nameof(ProjectFormViewModel.Methodology),
                "Please select a valid methodology.");
        }
    }

    private void ValidateDates(
        ProjectFormViewModel model)
    {
        if (model.EndDate.HasValue
            && model.EndDate.Value.Date
            < model.StartDate.Date)
        {
            ModelState.AddModelError(
                nameof(model.EndDate),
                "End date cannot be before start date.");
        }
    }

    private static void NormalizeModel(
        ProjectFormViewModel model)
    {
        model.Name =
            model.Name?.Trim()
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

    private static ProjectListItemViewModel
        MapToListItem(
            ProjectSummary project)
    {
        return new ProjectListItemViewModel
        {
            Id =
                project.Id,

            Name =
                project.Name,

            Description =
                project.Description,

            Methodology =
                project.Methodology,

            Status =
                project.Status,

            StartDate =
                project.StartDate,

            EndDate =
                project.EndDate,

            MemberCount =
                project.MemberCount,

            CreatedAt =
                project.CreatedAt
        };
    }
}