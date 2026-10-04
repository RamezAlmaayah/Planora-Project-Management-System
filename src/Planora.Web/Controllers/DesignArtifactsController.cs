using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Security;
using Planora.Application.Abstractions.VModel;
using Planora.Application.Common.VModel;
using Planora.Web.ViewModels.VModel;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class DesignArtifactsController : VModelArtifactControllerBase
{
    private readonly IVModelArtifactService _service;

    public DesignArtifactsController(IVModelArtifactService service,
        IProjectService projectService, IProjectAccessService projectAccessService)
        : base(projectService, projectAccessService) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Index(int projectId, string? search,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, false, cancellationToken);
        if (error is not null) return error;
        if (search?.Trim().Length > 200) return BadRequest("Search must be 200 characters or fewer.");
        return View(new DesignArtifactIndexViewModel
        {
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            IsReadOnly = scope.IsReadOnly,
            CanManage = !scope.IsReadOnly && CanManage(scope),
            Search = search?.Trim(),
            Artifacts = await _service.GetDesignArtifactsAsync(projectId, search, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int projectId, int id,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, false, cancellationToken);
        if (error is not null) return error;
        DesignArtifactDetails? artifact = await _service.GetDesignArtifactAsync(
            projectId, id, cancellationToken);
        if (artifact is null) return NotFound();
        return View(new DesignArtifactDetailsViewModel
        {
            Artifact = artifact,
            IsReadOnly = scope!.IsReadOnly,
            CanEdit = !scope.IsReadOnly && CanManage(scope)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int projectId, CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        return View(new DesignArtifactFormViewModel
        {
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            Identifier = await _service.GetNextDesignIdentifierPreviewAsync(projectId, cancellationToken),
            RequirementOptions = await _service.GetApprovedRequirementOptionsAsync(projectId,
                cancellationToken: cancellationToken)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int projectId, DesignArtifactFormViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        if (model.ProjectId != projectId || model.Id != 0)
            return BadRequest("The design artifact reference is invalid.");
        if (ModelState.IsValid)
        {
            ArtifactOperationResult result = await _service.CreateDesignArtifactAsync(new()
            {
                ProjectId = projectId,
                Title = model.Title,
                Description = model.Description,
                Type = model.Type,
                RequirementIds = model.RequirementIds,
                ActorUserId = scope!.UserId
            }, cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Details), new { projectId, id = result.ArtifactId });
            }
            if (result.Failure != ArtifactOperationFailure.Validation) return Failure(result);
            ModelState.AddModelError(string.Empty, result.Message);
        }
        model.ProjectName = scope!.Project.Name;
        model.Identifier = await _service.GetNextDesignIdentifierPreviewAsync(projectId, cancellationToken);
        model.RequirementOptions = await _service.GetApprovedRequirementOptionsAsync(projectId,
            cancellationToken: cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int projectId, int id, CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        DesignArtifactDetails? artifact = await _service.GetDesignArtifactAsync(projectId, id, cancellationToken);
        if (artifact is null) return NotFound();
        return View(new DesignArtifactFormViewModel
        {
            Id = artifact.Id,
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            Identifier = artifact.Identifier,
            Title = artifact.Title,
            Description = artifact.Description,
            Type = artifact.Type,
            RequirementIds = artifact.Requirements.Select(x => x.Id).ToList(),
            RequirementOptions = await _service.GetApprovedRequirementOptionsAsync(
                projectId, id, cancellationToken)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int projectId, int id,
        DesignArtifactFormViewModel model, CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        if (model.ProjectId != projectId || model.Id != id)
            return BadRequest("The design artifact reference is invalid.");
        if (ModelState.IsValid)
        {
            ArtifactOperationResult result = await _service.UpdateDesignArtifactAsync(new()
            {
                ProjectId = projectId,
                ArtifactId = id,
                Title = model.Title,
                Description = model.Description,
                Type = model.Type,
                RequirementIds = model.RequirementIds,
                ActorUserId = scope!.UserId
            }, cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Details), new { projectId, id });
            }
            if (result.Failure != ArtifactOperationFailure.Validation) return Failure(result);
            ModelState.AddModelError(string.Empty, result.Message);
        }
        DesignArtifactDetails? current = await _service.GetDesignArtifactAsync(projectId, id, cancellationToken);
        if (current is null) return NotFound();
        model.ProjectName = scope!.Project.Name;
        model.Identifier = current.Identifier;
        model.RequirementOptions = await _service.GetApprovedRequirementOptionsAsync(
            projectId, id, cancellationToken);
        return View(model);
    }
}
