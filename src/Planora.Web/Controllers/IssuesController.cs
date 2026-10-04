using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Issues;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Security;
using Planora.Application.Common.Issues;
using Planora.Application.Common.Projects;
using Planora.Application.Common.Security;
using Planora.Domain.Enums;
using Planora.Web.ViewModels.Issues;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class IssuesController : Controller
{
    private readonly IIssueService _issueService;
    private readonly IProjectService _projectService;
    private readonly IProjectAccessService _projectAccessService;
    private readonly ILogger<IssuesController> _logger;

    public IssuesController(
        IIssueService issueService,
        IProjectService projectService,
        IProjectAccessService projectAccessService,
        ILogger<IssuesController> logger)
    {
        _issueService = issueService;
        _projectService = projectService;
        _projectAccessService = projectAccessService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int projectId,
        string? search,
        IssueStatus? status,
        IssueSeverity? severity,
        PriorityLevel? priority,
        string? assignedUserId,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, false, cancellationToken);
        if (error is not null) return error;
        search = search?.Trim();
        if (search?.Length > 200)
            return BadRequest("Search must be 200 characters or fewer.");
        if (status.HasValue && !Enum.IsDefined(status.Value) ||
            severity.HasValue && !Enum.IsDefined(severity.Value) ||
            priority.HasValue && !Enum.IsDefined(priority.Value))
            return BadRequest("An issue filter is invalid.");

        return View(new IssueIndexViewModel
        {
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            IsReadOnly = scope.IsReadOnly,
            Search = search,
            Status = status,
            Severity = severity,
            Priority = priority,
            AssignedUserId = assignedUserId,
            Assignees = await _issueService.GetDeveloperOptionsAsync(
                projectId, cancellationToken),
            Issues = await _issueService.GetProjectIssuesAsync(
                projectId,
                search,
                new IssueFilter
                {
                    Status = status,
                    Severity = severity,
                    Priority = priority,
                    AssignedUserId = assignedUserId
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
        IssueDetails? issue = await _issueService.GetByIdAsync(
            projectId, id, cancellationToken);
        if (issue is null) return NotFound();

        bool assignedDeveloper = IsAssignedDeveloper(scope!, issue);
        bool qa = CanPerformQa(scope!);
        return View(new IssueDetailsViewModel
        {
            Issue = issue,
            IsReadOnly = scope!.IsReadOnly,
            CanEdit = !scope.IsReadOnly && CanEdit(scope, issue),
            CanAssign = !scope.IsReadOnly && CanManage(scope) &&
                issue.Status is IssueStatus.Open or IssueStatus.Assigned or IssueStatus.Reopened,
            CanStart = !scope.IsReadOnly && assignedDeveloper &&
                issue.Status is IssueStatus.Assigned or IssueStatus.Reopened,
            CanResolve = !scope.IsReadOnly && assignedDeveloper &&
                issue.Status == IssueStatus.InProgress,
            CanClose = !scope.IsReadOnly && qa && issue.Status == IssueStatus.Resolved,
            CanReopen = !scope.IsReadOnly && qa &&
                issue.Status is IssueStatus.Resolved or IssueStatus.Closed,
            Assignees = await _issueService.GetDeveloperOptionsAsync(
                projectId, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(
        int projectId,
        int? taskId,
        int? testExecutionId,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        var model = new IssueFormViewModel
        {
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            TaskItemId = taskId,
            VModelTestExecutionId = testExecutionId
        };
        await PopulateFormAsync(model, cancellationToken);
        if (taskId.HasValue && !model.TaskOptions.Any(option => option.TaskId == taskId.Value))
            return BadRequest("The selected task does not belong to this project.");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int projectId,
        IssueFormViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (model.Id != 0 || model.ProjectId != projectId)
            return BadRequest();

        if (ModelState.IsValid)
        {
            IssueOperationResult result = await _issueService.CreateAsync(
                new CreateIssueRequest
                {
                    ProjectId = projectId,
                    TaskItemId = model.TaskItemId,
                    VModelTestExecutionId = model.VModelTestExecutionId,
                    Title = model.Title,
                    Description = model.Description,
                    Severity = model.Severity,
                    Priority = model.Priority,
                    ReporterUserId = scope!.UserId
                }, cancellationToken);
            if (result.Succeeded)
            {
                _logger.LogInformation(
                    "Issue {IssueId} created in project {ProjectId} by user {UserId}.",
                    result.IssueId, projectId, scope.UserId);
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Details), new
                {
                    projectId,
                    id = result.IssueId
                });
            }
            if (result.Failure != IssueOperationFailure.Validation)
                return Failure(result);
            ModelState.AddModelError(string.Empty, result.Message);
        }

        model.ProjectName = scope!.Project.Name;
        await PopulateFormAsync(model, cancellationToken);
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
        IssueDetails? issue = await _issueService.GetByIdAsync(
            projectId, id, cancellationToken);
        if (issue is null) return NotFound();
        if (!CanEdit(scope!, issue)) return Forbid();

        var model = new IssueFormViewModel
        {
            Id = issue.Id,
            ProjectId = projectId,
            ProjectName = scope!.Project.Name,
            ExpectedStatus = issue.Status,
            Title = issue.Title,
            Description = issue.Description,
            Severity = issue.Severity,
            Priority = issue.Priority,
            TaskItemId = issue.TaskItemId
        };
        await PopulateFormAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
        int projectId,
        int id,
        IssueFormViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, true, cancellationToken);
        if (error is not null) return error;
        if (model.Id != id || model.ProjectId != projectId)
            return BadRequest();

        if (ModelState.IsValid)
        {
            IssueOperationResult result = await _issueService.UpdateAsync(
                new UpdateIssueRequest
                {
                    ProjectId = projectId,
                    IssueId = id,
                    ExpectedStatus = model.ExpectedStatus,
                    TaskItemId = model.TaskItemId,
                    Title = model.Title,
                    Description = model.Description,
                    Severity = model.Severity,
                    Priority = model.Priority,
                    ActorUserId = scope!.UserId
                }, cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = result.Message;
                return RedirectToAction(nameof(Details), new { projectId, id });
            }
            if (result.Failure != IssueOperationFailure.Validation)
                return Failure(result);
            ModelState.AddModelError(string.Empty, result.Message);
        }

        model.ProjectName = scope!.Project.Name;
        await PopulateFormAsync(model, cancellationToken);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(
        AssignIssueViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            model.ProjectId, true, cancellationToken);
        if (error is not null) return error;
        if (!CanManage(scope!)) return Forbid();
        if (!ModelState.IsValid) return BadRequest("Select a project Developer.");

        IssueOperationResult result = await _issueService.AssignAsync(
            new AssignIssueRequest
            {
                ProjectId = model.ProjectId,
                IssueId = model.IssueId,
                ExpectedStatus = model.ExpectedStatus,
                ExpectedAssignedUserId = model.ExpectedAssignedUserId,
                AssignedUserId = model.AssignedUserId,
                ActorUserId = scope!.UserId
            }, cancellationToken);
        return MutationResult(result, model.ProjectId, model.IssueId);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Transition(
        TransitionIssueViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            model.ProjectId, true, cancellationToken);
        if (error is not null) return error;
        if (!ModelState.IsValid) return BadRequest("The issue transition is invalid.");

        IssueOperationResult result = await _issueService.TransitionAsync(
            new TransitionIssueRequest
            {
                ProjectId = model.ProjectId,
                IssueId = model.IssueId,
                ExpectedStatus = model.ExpectedStatus,
                TargetStatus = model.TargetStatus,
                ResolutionNotes = model.ResolutionNotes,
                ActorUserId = scope!.UserId
            }, cancellationToken);
        return MutationResult(result, model.ProjectId, model.IssueId);
    }

    private async Task PopulateFormAsync(
        IssueFormViewModel model,
        CancellationToken cancellationToken)
    {
        model.TaskOptions = await _issueService.GetTaskOptionsAsync(
            model.ProjectId, cancellationToken);
    }

    private async Task<(IssueScope? Scope, IActionResult? Error)> GetScopeAsync(
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

        ProjectMemberRole? projectRole = isAdmin
            ? null
            : await _projectAccessService.GetProjectRoleAsync(
                projectId, userId, cancellationToken);
        var scope = new IssueScope(project, userId, projectRole);
        if (mutation && scope.IsReadOnly)
            return (null, StatusCode(
                StatusCodes.Status409Conflict,
                "Archived projects are read-only."));
        return (scope, null);
    }

    private bool CanManage(IssueScope scope) =>
        User.IsInRole(SystemRoles.Admin) ||
        User.IsInRole(SystemRoles.ProjectManager) &&
        scope.ProjectRole == ProjectMemberRole.ProjectManager ||
        User.IsInRole(SystemRoles.ScrumMaster) &&
        scope.ProjectRole == ProjectMemberRole.ScrumMaster;

    private bool CanEdit(IssueScope scope, IssueDetails issue) =>
        issue.Status is (IssueStatus.Open or IssueStatus.Assigned) &&
        (CanManage(scope) ||
         issue.Status == IssueStatus.Open && issue.ReporterUserId == scope.UserId);

    private bool IsAssignedDeveloper(IssueScope scope, IssueDetails issue) =>
        User.IsInRole(SystemRoles.Developer) &&
        scope.ProjectRole == ProjectMemberRole.Developer &&
        issue.AssignedUserId == scope.UserId;

    private bool CanPerformQa(IssueScope scope) =>
        User.IsInRole(SystemRoles.QaTester) &&
        !User.IsInRole(SystemRoles.Admin) &&
        !User.IsInRole(SystemRoles.ProjectManager) &&
        !User.IsInRole(SystemRoles.ScrumMaster) &&
        !User.IsInRole(SystemRoles.Developer) &&
        scope.ProjectRole == ProjectMemberRole.QaTester;

    private IActionResult MutationResult(
        IssueOperationResult result,
        int projectId,
        int issueId)
    {
        if (!result.Succeeded) return Failure(result);
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { projectId, id = issueId });
    }

    private IActionResult Failure(IssueOperationResult result) => result.Failure switch
    {
        IssueOperationFailure.NotFound => NotFound(result.Message),
        IssueOperationFailure.Forbidden => StatusCode(
            StatusCodes.Status403Forbidden, result.Message),
        IssueOperationFailure.ReadOnly or IssueOperationFailure.Conflict => StatusCode(
            StatusCodes.Status409Conflict, result.Message),
        _ => BadRequest(result.Message)
    };

    private sealed record IssueScope(
        ProjectSummary Project,
        string UserId,
        ProjectMemberRole? ProjectRole)
    {
        public bool IsReadOnly => Project.Status == ProjectStatus.Archived;
    }
}
