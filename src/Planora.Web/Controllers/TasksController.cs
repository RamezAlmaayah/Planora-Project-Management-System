using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Scrum;
using Planora.Application.Abstractions.Security;
using Planora.Application.Abstractions.Tasks;
using Planora.Application.Common.Projects;
using Planora.Application.Common.Scrum.Sprints;
using Planora.Application.Common.Security;
using Planora.Application.Common.Tasks;
using Planora.Domain.Enums;
using Planora.Web.ViewModels.Tasks;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class TasksController : Controller
{
    private readonly ITaskService _taskService;
    private readonly IProjectService _projectService;
    private readonly ISprintService _sprintService;
    private readonly IProjectAccessService _projectAccessService;
    private readonly ILogger<TasksController> _logger;
    private readonly ITaskCollaborationService? _taskCollaborationService;

    public TasksController(ITaskService taskService, IProjectService projectService,
        ISprintService sprintService, IProjectAccessService projectAccessService,
        ILogger<TasksController> logger,
        ITaskCollaborationService? taskCollaborationService = null)
    {
        _taskService = taskService;
        _projectService = projectService;
        _sprintService = sprintService;
        _projectAccessService = projectAccessService;
        _logger = logger;
        _taskCollaborationService = taskCollaborationService;
    }

    [HttpGet]
    public async Task<IActionResult> Board(int projectId, int sprintId,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, sprintId, false, cancellationToken);
        if (error is not null) return error;

        var tasks = await _taskService.GetSprintBoardTasksAsync(projectId, sprintId, cancellationToken);
        TaskScope resolvedScope = scope!;
        TaskTransitionActor? actor = GetTransitionActor(resolvedScope);
        bool canPerformQa = CanPerformQa(resolvedScope);
        return View(new TaskBoardViewModel
        {
            ProjectId = projectId,
            SprintId = sprintId,
            ProjectName = resolvedScope.Project.Name,
            SprintName = resolvedScope.Sprint.Name,
            IsReadOnly = resolvedScope.IsReadOnly,
            Tasks = tasks.Select(task => new TaskBoardCardViewModel
            {
                Id = task.Id,
                Title = task.Title,
                BacklogTitle = task.BacklogTitle,
                Priority = task.Priority,
                Status = task.Status,
                AssigneeName = task.AssigneeName,
                Deadline = task.Deadline,
                CanTransition = !resolvedScope.IsReadOnly &&
                    task.BacklogStatus == BacklogItemStatus.InSprint &&
                    task.Status is TaskItemStatus.ToDo or TaskItemStatus.InProgress &&
                    (actor is TaskTransitionActor.Admin or
                        TaskTransitionActor.ProjectManager or TaskTransitionActor.ScrumMaster ||
                     actor == TaskTransitionActor.Developer && task.AssignedUserId == resolvedScope.UserId),
                CanQaReview = !resolvedScope.IsReadOnly &&
                    canPerformQa &&
                    task.Status == TaskItemStatus.InReview
            }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(
        [FromBody] TaskStatusUpdateViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            model.ProjectId, model.SprintId, false, cancellationToken);
        if (error is not null) return TransitionScopeError(error);

        TaskTransitionActor? actor = GetTransitionActor(scope!);
        if (!actor.HasValue)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                succeeded = false,
                message = "You are not authorized to change task status."
            });
        }

        TaskStatusTransitionResult result = await _taskService.TransitionStatusAsync(
            new TaskStatusTransitionRequest
            {
                ProjectId = model.ProjectId,
                SprintId = model.SprintId,
                TaskId = model.TaskId,
                CurrentStatus = model.CurrentStatus,
                TargetStatus = model.TargetStatus,
                ActorUserId = scope!.UserId,
                Actor = actor.Value
            }, cancellationToken);

        if (!result.Succeeded)
        {
            int statusCode = result.Failure switch
            {
                TaskStatusTransitionFailure.NotFound => StatusCodes.Status404NotFound,
                TaskStatusTransitionFailure.Forbidden => StatusCodes.Status403Forbidden,
                TaskStatusTransitionFailure.Conflict or TaskStatusTransitionFailure.ReadOnly
                    => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            return StatusCode(statusCode, new { succeeded = false, message = result.Message });
        }

        _logger.LogInformation(
            "Task {TaskId} moved from {CurrentStatus} to {TargetStatus} in project {ProjectId}, sprint {SprintId} by user {UserId}.",
            model.TaskId, model.CurrentStatus, model.TargetStatus,
            model.ProjectId, model.SprintId, scope!.UserId);

        return Ok(new
        {
            succeeded = true,
            message = result.Message,
            status = result.Status
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> QaReview(
        [FromForm] QaReviewTaskViewModel model,
        CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return StatusCode(StatusCodes.Status401Unauthorized, new
            {
                succeeded = false,
                message = "Authentication is required."
            });
        }

        if (!HasQaOnlyGlobalRole())
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                succeeded = false,
                message = "Only QA Testers can perform QA reviews."
            });
        }

        var (scope, error) = await GetScopeAsync(
            model.ProjectId, model.SprintId, false, cancellationToken);
        if (error is not null) return TransitionScopeError(error);
        if (!CanPerformQa(scope!))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                succeeded = false,
                message = "You must be a QA Tester in this project to review this task."
            });
        }

        var evidenceUploads = model.EvidenceFiles.Select(file =>
        {
            var stream = file.OpenReadStream();
            return new QaEvidenceUpload
            {
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                Content = stream
            };
        }).ToArray();
        QaReviewOperationResult result;
        try
        {
            result = await _taskService.ReviewAsync(
                new QaReviewTaskRequest
                {
                    ProjectId = model.ProjectId,
                    SprintId = model.SprintId,
                    TaskId = model.TaskId,
                    ReviewerUserId = scope!.UserId,
                    Result = model.Result,
                    Notes = model.Notes,
                    EvidenceFiles = evidenceUploads
                }, cancellationToken);
        }
        finally
        {
            foreach (var evidence in evidenceUploads)
                await evidence.Content.DisposeAsync();
        }

        if (!result.Succeeded)
        {
            int statusCode = result.Failure switch
            {
                QaReviewOperationFailure.NotFound => StatusCodes.Status404NotFound,
                QaReviewOperationFailure.Forbidden => StatusCodes.Status403Forbidden,
                QaReviewOperationFailure.Conflict or QaReviewOperationFailure.ReadOnly
                    => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status400BadRequest
            };
            return StatusCode(statusCode, new { succeeded = false, message = result.Message });
        }

        _logger.LogInformation(
            "QA review {ReviewId} recorded as {Result} for task {TaskId} in project {ProjectId}, sprint {SprintId} by user {UserId}.",
            result.ReviewId, model.Result, model.TaskId,
            model.ProjectId, model.SprintId, scope!.UserId);

        return Ok(new
        {
            succeeded = true,
            message = result.Message,
            status = result.TaskStatus,
            backlogStatus = result.BacklogStatus,
            reviewId = result.ReviewId
        });
    }

    [HttpGet]
    public async Task<IActionResult> Index(int projectId, int sprintId, int page = 1,
        string? search = null, CancellationToken cancellationToken = default)
    {
        var (scope, error) = await GetScopeAsync(projectId, sprintId, false, cancellationToken);
        if (error is not null) return error;

        search = search?.Trim();
        if (search?.Length > 200) return BadRequest("Search must be 200 characters or fewer.");
        return View(new TaskIndexViewModel
        {
            ProjectId = projectId,
            SprintId = sprintId,
            ProjectName = scope!.Project.Name,
            SprintName = scope.Sprint.Name,
            CanManage = scope.CanMutate,
            IsReadOnly = scope.IsReadOnly,
            Search = search,
            Tasks = await _taskService.GetSprintTasksAsync(projectId, sprintId, page, search, cancellationToken)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int projectId, int sprintId, int id,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, sprintId, false, cancellationToken);
        if (error is not null) return error;
        var task = await _taskService.GetByIdAsync(projectId, sprintId, id, cancellationToken);
        if (task is null) return NotFound();

        return View(new TaskDetailsViewModel
        {
            ProjectName = scope!.Project.Name,
            SprintName = scope.Sprint.Name,
            CanEdit = scope.CanMutate && task.BacklogStatus == BacklogItemStatus.InSprint,
            IsReadOnly = scope.IsReadOnly || task.BacklogStatus != BacklogItemStatus.InSprint,
            CurrentUserId = scope.UserId,
            IsAdmin = User.IsInRole(SystemRoles.Admin),
            CanUploadAttachment = !scope.IsReadOnly && CanUploadAttachment(scope),
            CanDeleteAnyAttachment = !scope.IsReadOnly &&
                (User.IsInRole(SystemRoles.Admin) ||
                 User.IsInRole(SystemRoles.ProjectManager) &&
                 scope.ProjectRole == ProjectMemberRole.ProjectManager),
            CanDeleteOwnAttachment = !scope.IsReadOnly &&
                User.IsInRole(SystemRoles.ScrumMaster) &&
                scope.ProjectRole == ProjectMemberRole.ScrumMaster,
            CanReportIssue = scope.Project.Status != ProjectStatus.Archived,
            Task = task
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(
        int projectId,
        int sprintId,
        int id,
        string? content,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            projectId, sprintId, false, cancellationToken);
        if (error is not null) return error;
        if (_taskCollaborationService is null) return StatusCode(500);

        TaskCollaborationResult result = await _taskCollaborationService.AddCommentAsync(
            new AddTaskCommentRequest
            {
                ProjectId = projectId,
                SprintId = sprintId,
                TaskId = id,
                AuthorUserId = scope!.UserId,
                Content = content ?? string.Empty
            }, cancellationToken);
        return CollaborationMutationResult(result, projectId, sprintId, id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteComment(
        int projectId,
        int sprintId,
        int id,
        int commentId,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            projectId, sprintId, false, cancellationToken);
        if (error is not null) return error;
        if (_taskCollaborationService is null) return StatusCode(500);

        TaskCollaborationResult result = await _taskCollaborationService.DeleteCommentAsync(
            new DeleteTaskCommentRequest
            {
                ProjectId = projectId,
                SprintId = sprintId,
                TaskId = id,
                CommentId = commentId,
                ActorUserId = scope!.UserId
            }, cancellationToken);
        return CollaborationMutationResult(result, projectId, sprintId, id);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(11 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 11 * 1024 * 1024)]
    public async Task<IActionResult> UploadAttachment(
        int projectId,
        int sprintId,
        int id,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            projectId, sprintId, false, cancellationToken);
        if (error is not null) return error;
        if (!CanUploadAttachment(scope!)) return Forbid();
        if (_taskCollaborationService is null) return StatusCode(500);
        if (file is null) return BadRequest("Select a file to upload.");

        await using Stream content = file.OpenReadStream();
        TaskCollaborationResult result = await _taskCollaborationService.UploadAttachmentAsync(
            new UploadTaskAttachmentRequest
            {
                ProjectId = projectId,
                SprintId = sprintId,
                TaskId = id,
                UploadedByUserId = scope!.UserId,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                FileSizeBytes = file.Length,
                Content = content
            }, cancellationToken);
        return CollaborationMutationResult(result, projectId, sprintId, id);
    }

    [HttpGet]
    public async Task<IActionResult> DownloadAttachment(
        int projectId,
        int sprintId,
        int id,
        int attachmentId,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            projectId, sprintId, false, cancellationToken);
        if (error is not null) return error;
        if (_taskCollaborationService is null) return StatusCode(500);

        TaskAttachmentDownloadResult result =
            await _taskCollaborationService.DownloadAttachmentAsync(
                new DownloadTaskAttachmentRequest
                {
                    ProjectId = projectId,
                    SprintId = sprintId,
                    TaskId = id,
                    AttachmentId = attachmentId,
                    ActorUserId = scope!.UserId
                }, cancellationToken);
        if (!result.Succeeded)
            return CollaborationFailureResult(result.Failure, result.Message);
        return File(result.Content!, result.ContentType!, result.FileName!);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAttachment(
        int projectId,
        int sprintId,
        int id,
        int attachmentId,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(
            projectId, sprintId, false, cancellationToken);
        if (error is not null) return error;
        if (_taskCollaborationService is null) return StatusCode(500);

        TaskCollaborationResult result = await _taskCollaborationService.DeleteAttachmentAsync(
            new DeleteTaskAttachmentRequest
            {
                ProjectId = projectId,
                SprintId = sprintId,
                TaskId = id,
                AttachmentId = attachmentId,
                ActorUserId = scope!.UserId
            }, cancellationToken);
        return CollaborationMutationResult(result, projectId, sprintId, id);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int projectId, int sprintId, int? sprintBacklogItemId,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, sprintId, true, cancellationToken);
        if (error is not null) return error;
        var model = new TaskFormViewModel
        {
            ProjectId = projectId,
            SprintId = sprintId,
            SprintBacklogItemId = sprintBacklogItemId ?? 0
        };
        await PopulateFormAsync(model, scope!, cancellationToken);
        if (sprintBacklogItemId.HasValue && !model.BacklogOptions.Any(
                x => x.SprintBacklogItemId == sprintBacklogItemId.Value))
            return BadRequest("Select an eligible backlog item in this sprint.");
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int projectId, int sprintId, TaskFormViewModel model,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, sprintId, true, cancellationToken);
        if (error is not null) return error;
        if (model.ProjectId != projectId || model.SprintId != sprintId || model.Id != 0)
            return BadRequest();

        if (ModelState.IsValid)
        {
            var result = await _taskService.CreateAsync(new CreateTaskRequest
            {
                ProjectId = projectId,
                SprintId = sprintId,
                SprintBacklogItemId = model.SprintBacklogItemId,
                Title = model.Title,
                Description = model.Description,
                Priority = model.Priority,
                AssignedUserId = model.AssignedUserId,
                Deadline = model.Deadline,
                CreatedByUserId = scope!.UserId
            }, cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogInformation("Task {TaskId} created in project {ProjectId}, sprint {SprintId} by user {UserId}.",
                    result.TaskId, projectId, sprintId, scope!.UserId);
                TempData["SuccessMessage"] = "Task created successfully.";
                return RedirectToAction(nameof(Details), new { projectId, sprintId, id = result.TaskId });
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to create task.");
        }

        await PopulateFormAsync(model, scope!, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int projectId, int sprintId, int id,
        CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, sprintId, true, cancellationToken);
        if (error is not null) return error;
        var task = await _taskService.GetByIdAsync(projectId, sprintId, id, cancellationToken);
        if (task is null) return NotFound();
        if (task.BacklogStatus != BacklogItemStatus.InSprint)
            return BadRequest("Only tasks linked to an In Sprint backlog item can be edited.");

        var model = new TaskFormViewModel
        {
            Id = id,
            ProjectId = projectId,
            SprintId = sprintId,
            SprintBacklogItemId = task.SprintBacklogItemId,
            Title = task.Title,
            Description = task.Description,
            Priority = task.Priority,
            AssignedUserId = task.AssignedUserId,
            Deadline = task.Deadline
        };
        await PopulateFormAsync(model, scope!, cancellationToken);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int projectId, int sprintId, int id,
        TaskFormViewModel model, CancellationToken cancellationToken)
    {
        var (scope, error) = await GetScopeAsync(projectId, sprintId, true, cancellationToken);
        if (error is not null) return error;
        if (model.Id != id || model.ProjectId != projectId || model.SprintId != sprintId)
            return BadRequest();

        var task = await _taskService.GetByIdAsync(projectId, sprintId, id, cancellationToken);
        if (task is null) return NotFound();
        if (model.SprintBacklogItemId != task.SprintBacklogItemId)
            return BadRequest("The task's sprint backlog item cannot be changed.");
        if (task.BacklogStatus != BacklogItemStatus.InSprint)
            return BadRequest("Only tasks linked to an In Sprint backlog item can be edited.");

        if (ModelState.IsValid)
        {
            var result = await _taskService.UpdateAsync(new UpdateTaskRequest
            {
                Id = id,
                ProjectId = projectId,
                SprintId = sprintId,
                SprintBacklogItemId = task.SprintBacklogItemId,
                Title = model.Title,
                Description = model.Description,
                Priority = model.Priority,
                AssignedUserId = model.AssignedUserId,
                Deadline = model.Deadline,
                UpdatedByUserId = scope!.UserId
            }, cancellationToken);

            if (result.Succeeded)
            {
                _logger.LogInformation("Task {TaskId} updated in project {ProjectId}, sprint {SprintId} by user {UserId}.",
                    id, projectId, sprintId, scope!.UserId);
                TempData["SuccessMessage"] = "Task updated successfully.";
                return RedirectToAction(nameof(Details), new { projectId, sprintId, id });
            }
            ModelState.AddModelError(string.Empty, result.Error ?? "Unable to update task.");
        }

        await PopulateFormAsync(model, scope!, cancellationToken);
        return View(model);
    }

    private async Task PopulateFormAsync(TaskFormViewModel model, TaskScope scope,
        CancellationToken cancellationToken)
    {
        model.ProjectName = scope.Project.Name;
        model.SprintName = scope.Sprint.Name;
        model.BacklogOptions = await _taskService.GetBacklogOptionsAsync(
            scope.Project.Id, scope.Sprint.Id, cancellationToken);
        model.AssigneeOptions = await _taskService.GetAssigneeOptionsAsync(scope.Project.Id, cancellationToken);
        model.CanSave = scope.CanMutate && model.BacklogOptions.Count > 0;
    }

    private async Task<(TaskScope? Scope, IActionResult? Error)> GetScopeAsync(
        int projectId, int sprintId, bool mutation, CancellationToken cancellationToken)
    {
        string? userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(userId))
            return (null, Challenge());
        if (!SystemRoles.All.Any(User.IsInRole)) return (null, Forbid());

        bool isAdmin = User.IsInRole(SystemRoles.Admin);
        var project = await _projectService.GetAccessibleProjectByIdAsync(
            projectId, userId, isAdmin, cancellationToken);
        if (project is null)
        {
            bool exists = await _projectService.ProjectExistsAsync(projectId, cancellationToken);
            return (null, exists ? Forbid() : NotFound());
        }
        if (project.Methodology != ProjectMethodology.Scrum)
            return (null, BadRequest("Tasks are available only for Scrum projects."));

        var projectRole = isAdmin ? null : await _projectAccessService.GetProjectRoleAsync(
            projectId, userId, cancellationToken);
        bool canManage = isAdmin
            || (User.IsInRole(SystemRoles.ProjectManager) && projectRole == ProjectMemberRole.ProjectManager)
            || (User.IsInRole(SystemRoles.ScrumMaster) && projectRole == ProjectMemberRole.ScrumMaster);
        if (mutation && !canManage) return (null, Forbid());

        var sprint = await _sprintService.GetByIdAsync(projectId, sprintId, cancellationToken);
        if (sprint is null) return (null, NotFound());
        var scope = new TaskScope(project, sprint, userId, canManage, projectRole);
        if (mutation && scope.IsReadOnly)
            return (null, BadRequest("Archived projects and completed sprints are read-only."));
        return (scope, null);
    }

    private TaskTransitionActor? GetTransitionActor(TaskScope scope)
    {
        if (User.IsInRole(SystemRoles.Admin))
            return TaskTransitionActor.Admin;
        if (User.IsInRole(SystemRoles.ProjectManager) &&
            scope.ProjectRole == ProjectMemberRole.ProjectManager)
            return TaskTransitionActor.ProjectManager;
        if (User.IsInRole(SystemRoles.ScrumMaster) &&
            scope.ProjectRole == ProjectMemberRole.ScrumMaster)
            return TaskTransitionActor.ScrumMaster;
        if (User.IsInRole(SystemRoles.Developer) &&
            scope.ProjectRole == ProjectMemberRole.Developer)
            return TaskTransitionActor.Developer;
        return null;
    }

    private bool CanPerformQa(TaskScope scope)
    {
        return HasQaOnlyGlobalRole() &&
               scope.ProjectRole == ProjectMemberRole.QaTester;
    }

    private bool CanUploadAttachment(TaskScope scope)
    {
        return User.IsInRole(SystemRoles.Admin) ||
               User.IsInRole(SystemRoles.ProjectManager) &&
               scope.ProjectRole == ProjectMemberRole.ProjectManager ||
               User.IsInRole(SystemRoles.ScrumMaster) &&
               scope.ProjectRole == ProjectMemberRole.ScrumMaster;
    }

    private IActionResult CollaborationMutationResult(
        TaskCollaborationResult result,
        int projectId,
        int sprintId,
        int taskId)
    {
        if (!result.Succeeded)
            return CollaborationFailureResult(result.Failure, result.Message);

        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new
        {
            projectId,
            sprintId,
            id = taskId
        });
    }

    private IActionResult CollaborationFailureResult(
        TaskCollaborationFailure failure,
        string message)
    {
        return failure switch
        {
            TaskCollaborationFailure.NotFound => NotFound(message),
            TaskCollaborationFailure.Forbidden => StatusCode(
                StatusCodes.Status403Forbidden, message),
            TaskCollaborationFailure.ReadOnly => StatusCode(
                StatusCodes.Status409Conflict, message),
            TaskCollaborationFailure.Storage => StatusCode(
                StatusCodes.Status500InternalServerError,
                "The attachment operation could not be completed."),
            _ => BadRequest(message)
        };
    }

    private bool HasQaOnlyGlobalRole()
    {
        return User.IsInRole(SystemRoles.QaTester) &&
               !User.IsInRole(SystemRoles.Admin) &&
               !User.IsInRole(SystemRoles.ProjectManager) &&
               !User.IsInRole(SystemRoles.ScrumMaster) &&
               !User.IsInRole(SystemRoles.Developer);
    }

    private IActionResult TransitionScopeError(IActionResult error)
    {
        return error switch
        {
            ChallengeResult => StatusCode(StatusCodes.Status401Unauthorized,
                new { succeeded = false, message = "Authentication is required." }),
            ForbidResult => StatusCode(StatusCodes.Status403Forbidden,
                new { succeeded = false, message = "You cannot access this project." }),
            NotFoundResult => NotFound(new { succeeded = false, message = "Project or sprint was not found." }),
            BadRequestObjectResult badRequest => BadRequest(new
                { succeeded = false, message = badRequest.Value?.ToString() ?? "The request is invalid." }),
            _ => BadRequest(new { succeeded = false, message = "The request is invalid." })
        };
    }

    private sealed record TaskScope(
        ProjectSummary Project,
        SprintSummary Sprint,
        string UserId,
        bool CanManage,
        ProjectMemberRole? ProjectRole)
    {
        public bool IsReadOnly => Project.Status == ProjectStatus.Archived || Sprint.Status == SprintStatus.Completed;
        public bool CanMutate => CanManage && !IsReadOnly;
    }
}
