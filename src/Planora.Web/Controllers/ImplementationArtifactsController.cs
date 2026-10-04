using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Security;
using Planora.Application.Abstractions.VModel;
using Planora.Application.Common.VModel;
using Planora.Web.ViewModels.VModel;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class ImplementationArtifactsController : VModelArtifactControllerBase
{
    private readonly IVModelArtifactService _service;

    public ImplementationArtifactsController(IVModelArtifactService service,
        IProjectService projectService, IProjectAccessService projectAccessService)
        : base(projectService, projectAccessService) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Index(int projectId, string? search,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, false, cancellationToken);
        if (error is not null) return error;
        if (search?.Trim().Length > 200) return BadRequest("Search must be 200 characters or fewer.");
        return View(new ImplementationArtifactIndexViewModel
        {
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            IsReadOnly = scope.IsReadOnly,
            CanCreate = !scope.IsReadOnly && CanCreateImplementation(scope),
            Search = search?.Trim(),
            Artifacts = await _service.GetImplementationArtifactsAsync(
                projectId, search, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int projectId, int id,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, false, cancellationToken);
        if (error is not null) return error;
        ImplementationArtifactDetails? artifact = await _service.GetImplementationArtifactAsync(
            projectId, id, cancellationToken);
        if (artifact is null) return NotFound();
        return View(new ImplementationArtifactDetailsViewModel
        {
            Artifact = artifact,
            IsReadOnly = scope!.IsReadOnly,
            CanEdit = !scope.IsReadOnly && CanEditImplementation(scope, artifact.CreatorUserId)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(int projectId, CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanCreateImplementation(scope!)) return Forbid();
        return View(new ImplementationArtifactFormViewModel
        {
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            Identifier = await _service.GetNextImplementationIdentifierPreviewAsync(
                projectId, cancellationToken),
            DesignOptions = await _service.GetDesignArtifactOptionsAsync(projectId, cancellationToken)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int projectId,
        ImplementationArtifactFormViewModel model, CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanCreateImplementation(scope!)) return Forbid();
        if (model.ProjectId != projectId || model.Id != 0)
            return BadRequest("The implementation artifact reference is invalid.");
        if (ModelState.IsValid)
        {
            ArtifactOperationResult result = await _service.CreateImplementationArtifactAsync(new()
            {
                ProjectId = projectId,
                Title = model.Title,
                Description = model.Description,
                Type = model.Type,
                SourceReference = model.SourceReference,
                DesignArtifactIds = model.DesignArtifactIds,
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
        model.Identifier = await _service.GetNextImplementationIdentifierPreviewAsync(
            projectId, cancellationToken);
        model.DesignOptions = await _service.GetDesignArtifactOptionsAsync(projectId, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int projectId, int id,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        ImplementationArtifactDetails? artifact = await _service.GetImplementationArtifactAsync(
            projectId, id, cancellationToken);
        if (artifact is null) return NotFound();
        if (!CanEditImplementation(scope!, artifact.CreatorUserId)) return Forbid();
        return View(new ImplementationArtifactFormViewModel
        {
            Id = artifact.Id,
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            Identifier = artifact.Identifier,
            Title = artifact.Title,
            Description = artifact.Description,
            Type = artifact.Type,
            SourceReference = artifact.SourceReference,
            DesignArtifactIds = artifact.Designs.Select(x => x.Id).ToList(),
            DesignOptions = await _service.GetDesignArtifactOptionsAsync(projectId, cancellationToken)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int projectId, int id,
        ImplementationArtifactFormViewModel model, CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        ImplementationArtifactDetails? current = await _service.GetImplementationArtifactAsync(
            projectId, id, cancellationToken);
        if (current is null) return NotFound();
        if (!CanEditImplementation(scope!, current.CreatorUserId)) return Forbid();
        if (model.ProjectId != projectId || model.Id != id)
            return BadRequest("The implementation artifact reference is invalid.");
        if (ModelState.IsValid)
        {
            ArtifactOperationResult result = await _service.UpdateImplementationArtifactAsync(new()
            {
                ProjectId = projectId,
                ArtifactId = id,
                Title = model.Title,
                Description = model.Description,
                Type = model.Type,
                SourceReference = model.SourceReference,
                DesignArtifactIds = model.DesignArtifactIds,
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
        model.ProjectName = scope!.Project.Name;
        model.Identifier = current.Identifier;
        model.DesignOptions = await _service.GetDesignArtifactOptionsAsync(projectId, cancellationToken);
        return View(model);
    }
}
