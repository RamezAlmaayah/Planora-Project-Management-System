using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Planora.Application.Abstractions.Ai;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Requirements;
using Planora.Application.Abstractions.Security;
using Planora.Application.Common.Ai;
using Planora.Application.Common.Projects;
using Planora.Application.Common.Security;
using Planora.Domain.Enums;
using Planora.Web.ViewModels.Requirements;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class AiRequirementsController : Controller
{
    private readonly IAiRequirementGenerationService _generationService;
    private readonly IRequirementService _requirementService;
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _projectAccessService;

    public AiRequirementsController(
        IAiRequirementGenerationService generationService,
        IRequirementService requirementService,
        IProjectService projectService,
        IProjectAccessService projectAccessService)
    {
        _generationService = generationService;
        _requirementService = requirementService;
        _projectService = projectService;
        _projectAccessService = projectAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int projectId, CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, false, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        return View(BuildGeneratorModel(scope!, null));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.AiRequirementGeneration)]
    public async Task<IActionResult> Analyze(
        int projectId,
        AiRequirementGeneratorViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        AiScope authorizedScope = scope!;
        if (!CanManage(authorizedScope)) return Forbid();
        if (model.ProjectId != projectId)
            return BadRequest("The project reference is invalid.");
        if (!ModelState.IsValid)
            return View("Index", BuildGeneratorModel(authorizedScope, model));

        AiInputQualityAnalysisResult result = await _generationService.AnalyzeInputQualityAsync(new()
        {
            ProjectId = projectId,
            ActorUserId = authorizedScope.UserId,
            Mode = model.Mode,
            SuggestionCount = model.SuggestionCount,
            AdditionalContext = model.AdditionalContext
        }, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.Failure is AiRequirementFailure.Validation or
                AiRequirementFailure.ConfigurationUnavailable or
                AiRequirementFailure.TemporarilyUnavailable or
                AiRequirementFailure.InvalidResponse)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View("Index", BuildGeneratorModel(authorizedScope, model));
            }
            return Failure(result.Failure, result.Message);
        }

        return View("Index", BuildGeneratorModel(authorizedScope, model, result));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.AiRequirementGeneration)]
    public async Task<IActionResult> Generate(
        int projectId,
        AiRequirementGeneratorViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        AiScope authorizedScope = scope!;
        if (!CanManage(authorizedScope)) return Forbid();
        if (model.ProjectId != projectId)
            return BadRequest("The project reference is invalid.");
        if (!ModelState.IsValid)
            return View("Index", BuildGeneratorModel(authorizedScope, model));

        AiRequirementGenerationResult result = await _generationService.GenerateAsync(new()
        {
            ProjectId = projectId,
            ActorUserId = authorizedScope.UserId,
            Mode = model.Mode,
            SuggestionCount = model.SuggestionCount,
            AdditionalContext = model.AdditionalContext
        }, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.Failure is AiRequirementFailure.Validation or
                AiRequirementFailure.ConfigurationUnavailable or
                AiRequirementFailure.TemporarilyUnavailable or
                AiRequirementFailure.InvalidResponse)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View("Index", BuildGeneratorModel(
                    authorizedScope, model, result.QualityAnalysis));
            }
            return Failure(result.Failure, result.Message);
        }

        return View("Review", await BuildReviewModelAsync(
            projectId, authorizedScope.Project.Name, result, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveSelected(
        int projectId,
        AiRequirementReviewViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        AiScope authorizedScope = scope!;
        if (!CanManage(authorizedScope)) return Forbid();
        if (model.ProjectId != projectId)
            return BadRequest("The project reference is invalid.");

        List<AiFunctionalRequirementDraft> functional = model.FunctionalRequirements
            .Where(item => item.Include)
            .Select(item => new AiFunctionalRequirementDraft
            {
                RequirementName = item.RequirementName,
                Priority = item.Priority,
                Description = item.Description,
                BusinessRationale = item.BusinessRationale,
                Preconditions = item.Preconditions,
                ExceptionScenario = item.ExceptionScenario
            }).ToList();
        List<AiNonFunctionalRequirementDraft> nonFunctional = model.NonFunctionalRequirements
            .Where(item => item.Include)
            .Select(item => new AiNonFunctionalRequirementDraft
            {
                Category = item.Category,
                RequirementDescription = item.RequirementDescription,
                Priority = item.Priority,
                Rationale = item.Rationale,
                RelatedFunctionalRequirements = item.RelatedFunctionalRequirements
            }).ToList();
        AiRequirementSaveResult result = await _generationService.SaveSelectedAsync(new()
        {
            ProjectId = projectId,
            ActorUserId = authorizedScope.UserId,
            FunctionalRequirements = functional,
            NonFunctionalRequirements = nonFunctional
        }, cancellationToken);
        if (!result.Succeeded)
        {
            if (result.Failure == AiRequirementFailure.Validation)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                model.ProjectName = authorizedScope.Project.Name;
                model.FunctionalRequirementOptions = await _requirementService
                    .GetFunctionalRequirementOptionsAsync(projectId, cancellationToken);
                return View("Review", model);
            }
            return Failure(result.Failure, result.Message);
        }

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction("Index", "Requirements", new { projectId });
    }

    private AiRequirementGeneratorViewModel BuildGeneratorModel(
        AiScope scope,
        AiRequirementGeneratorViewModel? submitted,
        AiInputQualityAnalysisResult? quality = null)
    {
        ProjectSummary project = scope.Project;
        return new AiRequirementGeneratorViewModel
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            ProjectDescription = project.Description,
            ProjectObjectives = project.Objectives,
            ProjectScope = project.Scope,
            IsConfigured = _generationService.IsConfigured,
            IsReadOnly = scope.IsReadOnly,
            Mode = submitted?.Mode ?? AiRequirementGenerationMode.Both,
            SuggestionCount = submitted?.SuggestionCount ?? 3,
            AdditionalContext = submitted?.AdditionalContext,
            QualityAnalysis = quality is null ? null : new AiInputQualityAnalysisViewModel
            {
                QualityScore = quality.QualityScore,
                QualityLevel = quality.QualityLevel,
                ValidationMessage = quality.ValidationMessage,
                MissingInformation = quality.MissingInformation.ToList(),
                Issues = quality.Issues.ToList(),
                Suggestions = quality.Suggestions.ToList(),
                ImprovementItems = quality.ImprovementItems.Select(item => new AiInputImprovementViewModel
                {
                    Id = item.Id,
                    Topic = item.Topic,
                    Question = item.Question,
                    InputType = item.InputType,
                    Options = item.Options.ToList(),
                    SuggestedText = item.SuggestedText
                }).ToList(),
                IsSufficient = quality.IsSufficient
            }
        };
    }

    private async Task<AiRequirementReviewViewModel> BuildReviewModelAsync(
        int projectId,
        string projectName,
        AiRequirementGenerationResult result,
        CancellationToken cancellationToken) => new()
    {
        ProjectId = projectId,
        ProjectName = projectName,
        Warnings = result.Warnings.ToList(),
        FunctionalRequirements = result.FunctionalRequirements.Select(item => new AiFunctionalRequirementDraftViewModel
        {
            RequirementName = item.RequirementName,
            Priority = item.Priority,
            Description = item.Description,
            BusinessRationale = item.BusinessRationale,
            Preconditions = item.Preconditions,
            ExceptionScenario = item.ExceptionScenario
        }).ToList(),
        NonFunctionalRequirements = result.NonFunctionalRequirements.Select(item => new AiNonFunctionalRequirementDraftViewModel
        {
            Category = item.Category,
            RequirementDescription = item.RequirementDescription,
            Priority = item.Priority,
            Rationale = item.Rationale,
            RelatedFunctionalRequirements = item.RelatedFunctionalRequirements.ToList()
        }).ToList(),
        FunctionalRequirementOptions = await _requirementService
            .GetFunctionalRequirementOptionsAsync(projectId, cancellationToken)
    };

    private async Task<(AiScope? Scope, IActionResult? Error)> GetScopeAsync(
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
        var scope = new AiScope(project, userId, projectRole);
        if (mutation && scope.IsReadOnly)
            return (null, StatusCode(StatusCodes.Status409Conflict, "Archived projects are read-only."));
        return (scope, null);
    }

    private bool CanManage(AiScope scope) => User.IsInRole(SystemRoles.Admin) ||
        User.IsInRole(SystemRoles.ProjectManager) &&
        scope.ProjectRole == ProjectMemberRole.ProjectManager;
    private IActionResult Failure(AiRequirementFailure failure, string message) => failure switch
    {
        AiRequirementFailure.NotFound => NotFound(message),
        AiRequirementFailure.Forbidden => StatusCode(StatusCodes.Status403Forbidden, message),
        AiRequirementFailure.ReadOnly or AiRequirementFailure.Conflict =>
            StatusCode(StatusCodes.Status409Conflict, message),
        _ => BadRequest(message)
    };
    private sealed record AiScope(ProjectSummary Project, string UserId, ProjectMemberRole? ProjectRole)
    {
        public bool IsReadOnly => Project.Status == ProjectStatus.Archived;
    }
}
