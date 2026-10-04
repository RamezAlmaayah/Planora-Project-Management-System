using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Requirements;
using Planora.Application.Abstractions.Security;
using Planora.Application.Common.Projects;
using Planora.Application.Common.Requirements;
using Planora.Application.Common.Security;
using Planora.Domain.Enums;
using Planora.Web.ViewModels.Requirements;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class RequirementsController : Controller
{
    private readonly IRequirementService _requirementService;
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _projectAccessService;
    private readonly ILogger<RequirementsController> _logger;

    public RequirementsController(
        IRequirementService requirementService,
        IProjectService projectService,
        IProjectAccessService projectAccessService,
        ILogger<RequirementsController> logger)
    {
        _requirementService = requirementService;
        _projectService = projectService;
        _projectAccessService = projectAccessService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int projectId,
        string? search,
        RequirementType? type,
        RequirementStatus? status,
        PriorityLevel? priority,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, false, cancellationToken);
        if (error is not null) return error;
        search = search?.Trim();
        if (search?.Length > 200)
            return BadRequest("Search must be 200 characters or fewer.");
        if (type.HasValue && !Enum.IsDefined(type.Value) ||
            status.HasValue && !Enum.IsDefined(status.Value) ||
            priority.HasValue && !Enum.IsDefined(priority.Value))
            return BadRequest("A requirement filter is invalid.");

        return View(new RequirementIndexViewModel
        {
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            IsReadOnly = scope.IsReadOnly,
            CanManage = !scope.IsReadOnly && CanManage(scope),
            Search = search,
            Type = type,
            Status = status,
            Priority = priority,
            Requirements = await _requirementService.GetProjectRequirementsAsync(
                projectId,
                search,
                new RequirementFilter
                {
                    Type = type,
                    Status = status,
                    Priority = priority
                },
                cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int projectId,
        int id,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, false, cancellationToken);
        if (error is not null) return error;
        RequirementDetails? requirement = await _requirementService.GetByIdAsync(
            projectId, id, cancellationToken);
        if (requirement is null) return NotFound();

        bool canManage = !scope!.IsReadOnly && CanManage(scope);
        return View(new RequirementDetailsViewModel
        {
            Requirement = requirement,
            IsReadOnly = scope.IsReadOnly,
            CanManage = canManage,
            CanEdit = canManage && requirement.Status is
                RequirementStatus.Draft or RequirementStatus.Rejected,
            AllowedTargets = canManage
                ? AllowedTargets(requirement.Status)
                : [],
            DependencyOptions = canManage
                ? await _requirementService.GetDependencyOptionsAsync(
                    projectId, id, cancellationToken)
                : []
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(
        int projectId,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        var model = new CreateRequirementViewModel
        {
            ProjectId = projectId,
            ProjectName = scope!.Project.Name
        };
        await PopulateCreateFormAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int projectId,
        CreateRequirementViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        if (model.ProjectId != projectId)
            return BadRequest("The project reference is invalid.");

        if (ModelState.IsValid)
        {
            RequirementOperationResult result = await _requirementService.CreateAsync(
                new CreateRequirementRequest
                {
                    ProjectId = projectId,
                    Title = model.Title,
                    Description = model.Description,
                    Type = model.Type,
                    Priority = model.Priority,
                    Rationale = model.Rationale,
                    Preconditions = model.Preconditions,
                    ExceptionScenario = model.ExceptionScenario,
                    NfrCategory = model.NfrCategory,
                    RelatedFunctionalRequirementIds = model.RelatedFunctionalRequirementIds,
                    ActorUserId = scope!.UserId
                },
                cancellationToken);
            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Requirement {RequirementId} created in project {ProjectId} by {UserId}.",
                    result.RequirementId, projectId, scope.UserId);
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Details), new
                {
                    projectId,
                    id = result.RequirementId
                });
            }
            if (result.Failure != RequirementOperationFailure.Validation)
                return Failure(result);
            ModelState.AddModelError(string.Empty, result.Message);
        }

        model.ProjectName = scope!.Project.Name;
        await PopulateCreateFormAsync(model, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(
        int projectId,
        int id,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        RequirementDetails? requirement = await _requirementService.GetByIdAsync(
            projectId, id, cancellationToken);
        if (requirement is null) return NotFound();
        if (requirement.Status is not (RequirementStatus.Draft or RequirementStatus.Rejected))
            return StatusCode(
                StatusCodes.Status409Conflict,
                "Requirement content is read-only in its current status.");

        return View(new EditRequirementViewModel
        {
            Id = requirement.Id,
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            Identifier = requirement.Identifier,
            Type = requirement.Type,
            ExpectedStatus = requirement.Status,
            Title = requirement.Title,
            Description = requirement.Description,
            Priority = requirement.Priority,
            Rationale = requirement.Rationale,
            Preconditions = requirement.Preconditions,
            ExceptionScenario = requirement.ExceptionScenario,
            NfrCategory = requirement.NfrCategory,
            RelatedFunctionalRequirementIds = requirement.RelatedFunctionalRequirements
                .Select(item => item.RequirementId)
                .ToList(),
            FunctionalRequirementOptions = await _requirementService
                .GetFunctionalRequirementOptionsAsync(projectId, cancellationToken)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int projectId,
        int id,
        EditRequirementViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        if (model.Id != id || model.ProjectId != projectId)
            return BadRequest("The requirement reference is invalid.");

        if (ModelState.IsValid)
        {
            RequirementOperationResult result = await _requirementService.UpdateAsync(
                new UpdateRequirementRequest
                {
                    ProjectId = projectId,
                    RequirementId = id,
                    ExpectedStatus = model.ExpectedStatus,
                    Title = model.Title,
                    Description = model.Description,
                    Priority = model.Priority,
                    Rationale = model.Rationale,
                    Preconditions = model.Preconditions,
                    ExceptionScenario = model.ExceptionScenario,
                    NfrCategory = model.NfrCategory,
                    RelatedFunctionalRequirementIds = model.RelatedFunctionalRequirementIds,
                    ActorUserId = scope!.UserId
                },
                cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Details), new { projectId, id });
            }
            if (result.Failure != RequirementOperationFailure.Validation)
                return Failure(result);
            ModelState.AddModelError(string.Empty, result.Message);
        }

        RequirementDetails? current = await _requirementService.GetByIdAsync(
            projectId, id, cancellationToken);
        if (current is null) return NotFound();
        model.ProjectName = scope!.Project.Name;
        model.Identifier = current.Identifier;
        model.Type = current.Type;
        model.FunctionalRequirementOptions = await _requirementService
            .GetFunctionalRequirementOptionsAsync(projectId, cancellationToken);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(
        TransitionRequirementViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            model.ProjectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        if (!ModelState.IsValid)
            return BadRequest("The requirement transition is invalid.");

        RequirementOperationResult result = await _requirementService.TransitionAsync(
            new TransitionRequirementRequest
            {
                ProjectId = model.ProjectId,
                RequirementId = model.RequirementId,
                ExpectedStatus = model.ExpectedStatus,
                TargetStatus = model.TargetStatus,
                ActorUserId = scope!.UserId
            },
            cancellationToken);
        return MutationResult(result, model.ProjectId, model.RequirementId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDependency(
        AddRequirementDependencyViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            model.ProjectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        if (!ModelState.IsValid)
            return BadRequest("Select a valid project requirement.");

        RequirementOperationResult result = await _requirementService.AddDependencyAsync(
            new AddRequirementDependencyRequest
            {
                ProjectId = model.ProjectId,
                RequirementId = model.RequirementId,
                DependsOnRequirementId = model.DependsOnRequirementId,
                ActorUserId = scope!.UserId
            },
            cancellationToken);
        return MutationResult(result, model.ProjectId, model.RequirementId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDependency(
        RemoveRequirementDependencyViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            model.ProjectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();

        RequirementOperationResult result = await _requirementService.RemoveDependencyAsync(
            new RemoveRequirementDependencyRequest
            {
                ProjectId = model.ProjectId,
                RequirementId = model.RequirementId,
                DependencyId = model.DependencyId,
                ActorUserId = scope!.UserId
            },
            cancellationToken);
        return MutationResult(result, model.ProjectId, model.RequirementId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTrace(
        AddRequirementTraceViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            model.ProjectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        if (!ModelState.IsValid)
            return BadRequest("The traceability reference is invalid.");

        RequirementOperationResult result = await _requirementService.AddTraceAsync(
            new AddRequirementTraceRequest
            {
                ProjectId = model.ProjectId,
                RequirementId = model.RequirementId,
                Stage = model.Stage,
                ReferenceCode = model.ReferenceCode,
                Description = model.Description,
                ActorUserId = scope!.UserId
            },
            cancellationToken);
        return MutationResult(result, model.ProjectId, model.RequirementId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveTrace(
        RemoveRequirementTraceViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            model.ProjectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();

        RequirementOperationResult result = await _requirementService.RemoveTraceAsync(
            new RemoveRequirementTraceRequest
            {
                ProjectId = model.ProjectId,
                RequirementId = model.RequirementId,
                TraceId = model.TraceId,
                ActorUserId = scope!.UserId
            },
            cancellationToken);
        return MutationResult(result, model.ProjectId, model.RequirementId);
    }

    [HttpGet]
    public async Task<IActionResult> Traceability(
        int projectId,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, false, cancellationToken);
        if (error is not null) return error;
        return View(new RequirementTraceabilityViewModel
        {
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            Requirements = await _requirementService.GetTraceabilityAsync(
                projectId, cancellationToken)
        });
    }

    private async Task<(RequirementScope? Scope, IActionResult? Error)> GetScopeAsync(
        int projectId,
        bool mutation,
        CancellationToken cancellationToken)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(userId))
            return (null, Challenge());
        if (!SystemRoles.All.Any(User.IsInRole))
            return (null, Forbid());

        bool isAdmin = User.IsInRole(SystemRoles.Admin);
        ProjectSummary? project = await _projectService.GetAccessibleProjectByIdAsync(
            projectId, userId, isAdmin, cancellationToken);
        if (project is null)
        {
            bool exists = await _projectService.ProjectExistsAsync(
                projectId, cancellationToken);
            return (null, exists ? Forbid() : NotFound());
        }
        if (project.Methodology != ProjectMethodology.VModel)
            return (null, NotFound());

        ProjectMemberRole? projectRole = isAdmin
            ? null
            : await _projectAccessService.GetProjectRoleAsync(
                projectId, userId, cancellationToken);
        var scope = new RequirementScope(project, userId, projectRole);
        if (mutation && scope.IsReadOnly)
            return (null, StatusCode(
                StatusCodes.Status409Conflict,
                "Archived projects are read-only."));
        return (scope, null);
    }

    private async Task PopulateCreateFormAsync(
        CreateRequirementViewModel model,
        CancellationToken cancellationToken)
    {
        model.FunctionalRequirementOptions = await _requirementService
            .GetFunctionalRequirementOptionsAsync(model.ProjectId, cancellationToken);
        model.FunctionalIdentifierPreview = await _requirementService
            .GetIdentifierPreviewAsync(
                model.ProjectId,
                RequirementType.Functional,
                cancellationToken);
        model.NonFunctionalIdentifierPreview = await _requirementService
            .GetIdentifierPreviewAsync(
                model.ProjectId,
                RequirementType.NonFunctional,
                cancellationToken);
    }

    private bool CanManage(RequirementScope scope) =>
        User.IsInRole(SystemRoles.Admin) ||
        User.IsInRole(SystemRoles.ProjectManager) &&
        scope.ProjectRole == ProjectMemberRole.ProjectManager;

    private static IReadOnlyList<RequirementStatus> AllowedTargets(
        RequirementStatus status) => status switch
        {
            RequirementStatus.Draft => [RequirementStatus.UnderReview],
            RequirementStatus.UnderReview =>
                [RequirementStatus.Approved, RequirementStatus.Rejected],
            RequirementStatus.Rejected => [RequirementStatus.Draft],
            RequirementStatus.Approved => [RequirementStatus.Deprecated],
            _ => []
        };

    private IActionResult MutationResult(
        RequirementOperationResult result,
        int projectId,
        int requirementId)
    {
        if (!result.Succeeded) return Failure(result);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { projectId, id = requirementId });
    }

    private IActionResult Failure(RequirementOperationResult result) =>
        result.Failure switch
        {
            RequirementOperationFailure.NotFound => NotFound(result.Message),
            RequirementOperationFailure.Forbidden => StatusCode(
                StatusCodes.Status403Forbidden, result.Message),
            RequirementOperationFailure.ReadOnly or RequirementOperationFailure.Conflict =>
                StatusCode(StatusCodes.Status409Conflict, result.Message),
            _ => BadRequest(result.Message)
        };

    private sealed record RequirementScope(
        ProjectSummary Project,
        string UserId,
        ProjectMemberRole? ProjectRole)
    {
        public bool IsReadOnly => Project.Status == ProjectStatus.Archived;
    }
}
