using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Notifications;
using Planora.Application.Abstractions.Tasks;
using Planora.Application.Common.Notifications;
using Planora.Application.Common.Tasks;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Tasks;

public sealed class TaskService : ITaskService
{
    private const int PageSize = 20;
    private readonly ApplicationDbContext _context;
    private readonly ITaskAssignmentService _taskAssignmentService;
    private readonly IClock _clock;
    private readonly INotificationService? _notifications;
    private readonly IQaEvidenceStorage? _qaEvidenceStorage;

    public TaskService(
        ApplicationDbContext context,
        ITaskAssignmentService taskAssignmentService,
        IClock clock,
        INotificationService? notifications = null,
        IQaEvidenceStorage? qaEvidenceStorage = null)
    {
        _context = context;
        _taskAssignmentService = taskAssignmentService;
        _clock = clock;
        _notifications = notifications;
        _qaEvidenceStorage = qaEvidenceStorage;
    }

    public async Task<TaskListResult> GetSprintTasksAsync(
        int projectId,
        int sprintId,
        int page = 1,
        string? search = null,
        CancellationToken cancellationToken = default)
    {
        var query = GetTaskDetailsQuery(projectId, sprintId);
        string normalizedSearch = search?.Trim() ?? string.Empty;
        if (normalizedSearch.Length > 200)
            normalizedSearch = normalizedSearch[..200];

        if (normalizedSearch.Length > 0)
            query = query.Where(task => task.Title.Contains(normalizedSearch));

        int totalCount = await query.CountAsync(cancellationToken);
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / PageSize));
        int currentPage = Math.Clamp(page, 1, totalPages);
        var items = await query
            .OrderByDescending(task => task.CreatedAt)
            .ThenByDescending(task => task.Id)
            .Skip((currentPage - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

        return new TaskListResult
        {
            Items = items,
            Page = currentPage,
            PageSize = PageSize,
            TotalCount = totalCount
        };
    }

    public async Task<TaskDetails?> GetByIdAsync(
        int projectId,
        int sprintId,
        int taskId,
        CancellationToken cancellationToken = default)
    {
        TaskDetails? task = await GetTaskDetailsQuery(projectId, sprintId)
            .FirstOrDefaultAsync(item => item.Id == taskId, cancellationToken);
        if (task is null)
            return null;

        task.QaReviews = await _context.QaReviews.AsNoTracking()
            .Where(review => review.TaskItemId == taskId)
            .OrderByDescending(review => review.TestedAt)
            .ThenByDescending(review => review.Id)
            .Select(review => new QaReviewSummary
            {
                Id = review.Id,
                TaskItemId = review.TaskItemId,
                ReviewerUserId = review.QaUserId,
                ReviewerName = _context.Users
                    .Where(user => user.Id == review.QaUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName
                        : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? review.QaUserId,
                Result = review.Result,
                Notes = review.Notes,
                TestedAt = review.TestedAt,
                EvidenceFiles = _context.QaEvidenceFiles.AsNoTracking()
                    .Where(file => file.QaReviewId == review.Id)
                    .OrderBy(file => file.Id)
                    .Select(file => new QaEvidenceSummary
                    {
                        Id = file.Id,
                        OriginalFileName = file.OriginalFileName,
                        FileSizeBytes = file.FileSizeBytes,
                        Extension = file.Extension,
                        UploadedByUserId = file.UploadedByUserId,
                        UploadedByName = _context.Users.Where(user => user.Id == file.UploadedByUserId)
                            .Select(user => user.FullName != string.Empty ? user.FullName : user.UserName ?? "User")
                            .FirstOrDefault() ?? "User",
                        UploadedAt = file.UploadedAt
                    }).ToList()
            })
            .ToListAsync(cancellationToken);

        task.Comments = await _context.TaskComments.AsNoTracking()
            .Where(comment => comment.TaskItemId == taskId)
            .OrderBy(comment => comment.CreatedAt)
            .ThenBy(comment => comment.Id)
            .Select(comment => new TaskCommentSummary
            {
                Id = comment.Id,
                AuthorUserId = comment.AuthorUserId,
                AuthorName = _context.Users
                    .Where(user => user.Id == comment.AuthorUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName
                        : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? comment.AuthorUserId,
                Content = comment.Content,
                CreatedAt = comment.CreatedAt
            })
            .ToListAsync(cancellationToken);

        task.Attachments = await _context.TaskAttachments.AsNoTracking()
            .Where(attachment => attachment.TaskItemId == taskId)
            .OrderByDescending(attachment => attachment.UploadedAt)
            .ThenByDescending(attachment => attachment.Id)
            .Select(attachment => new TaskAttachmentSummary
            {
                Id = attachment.Id,
                OriginalFileName = attachment.OriginalFileName,
                FileSizeBytes = attachment.FileSizeBytes,
                Extension = attachment.Extension,
                UploadedByUserId = attachment.UploadedByUserId,
                UploadedByName = _context.Users
                    .Where(user => user.Id == attachment.UploadedByUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName
                        : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? attachment.UploadedByUserId,
                UploadedAt = attachment.UploadedAt
            })
            .ToListAsync(cancellationToken);

        return task;
    }

    public async Task<IReadOnlyList<TaskSummary>> GetSprintBoardTasksAsync(
        int projectId,
        int sprintId,
        CancellationToken cancellationToken = default)
    {
        return await GetTaskDetailsQuery(projectId, sprintId)
            .OrderBy(task => task.Status)
            .ThenBy(task => task.Priority)
            .ThenBy(task => task.Deadline)
            .ThenBy(task => task.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskBacklogOption>> GetBacklogOptionsAsync(
        int projectId,
        int sprintId,
        CancellationToken cancellationToken = default)
    {
        return await _context.SprintBacklogItems
            .AsNoTracking()
            .Where(item =>
                item.SprintId == sprintId &&
                item.Sprint.ProjectId == projectId &&
                item.Sprint.Project.Methodology == ProjectMethodology.Scrum &&
                item.BacklogItem.ProjectId == projectId &&
                item.BacklogItem.Status == BacklogItemStatus.InSprint)
            .OrderBy(item => item.BacklogItem.Title)
            .ThenBy(item => item.Id)
            .Select(item => new TaskBacklogOption
            {
                SprintBacklogItemId = item.Id,
                Title = item.BacklogItem.Title
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TaskAssigneeOption>> GetAssigneeOptionsAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from member in _context.ProjectMembers.AsNoTracking()
            join project in _context.Projects.AsNoTracking()
                on member.ProjectId equals project.Id
            join user in _context.Users.AsNoTracking()
                on member.UserId equals user.Id
            where project.Id == projectId &&
                project.Methodology == ProjectMethodology.Scrum &&
                !user.IsDisabled
            select new TaskAssigneeOption
            {
                UserId = user.Id,
                DisplayName = user.FullName != string.Empty
                    ? user.FullName
                    : user.UserName ?? user.Email ?? user.Id,
                Role = member.Role
            })
            .OrderBy(option => option.DisplayName)
            .ThenBy(option => option.UserId)
            .ToListAsync(cancellationToken);
    }

    public async Task<TaskOperationResult> CreateAsync(
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? fieldError = ValidateFields(
            request.Title, request.Description, request.Priority,
            request.AssignedUserId, request.CreatedByUserId);
        if (fieldError is not null)
            return TaskOperationResult.Failure(fieldError);

        await using var transaction = await BeginMutationTransactionAsync(cancellationToken);
        string? contextError = await ValidateMutationContextAsync(
            request.ProjectId, request.SprintId, request.SprintBacklogItemId,
            cancellationToken);
        if (contextError is not null)
            return TaskOperationResult.Failure(contextError);

        string? assignedUserId = NormalizeAssignee(request.AssignedUserId);
        var assignmentResult = await _taskAssignmentService.ValidateForSprintBacklogItemAsync(
            request.SprintBacklogItemId, assignedUserId, cancellationToken);
        if (!assignmentResult.Succeeded || assignmentResult.ProjectId != request.ProjectId)
            return TaskOperationResult.Failure(
                assignmentResult.ErrorMessage ?? "The selected assignee is not eligible for this project.");

        if (assignedUserId is not null &&
            !await _context.Users.AsNoTracking().AnyAsync(
                user => user.Id == assignedUserId, cancellationToken))
            return TaskOperationResult.Failure("The selected assignee was not found.");

        var task = new TaskItem
        {
            SprintBacklogItemId = request.SprintBacklogItemId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            Status = TaskItemStatus.ToDo,
            AssignedUserId = assignedUserId,
            Deadline = request.Deadline,
            CreatedByUserId = request.CreatedByUserId.Trim(),
            CreatedAt = _clock.UtcNow
        };

        _context.TaskItems.Add(task);
        if (assignedUserId is not null)
        {
            await NotifyUserAsync(
                assignedUserId,
                request.ProjectId,
                NotificationType.TaskAssigned,
                "Task assigned",
                $"You were assigned the task “{task.Title}”.",
                TaskBoardUrl(request.ProjectId, request.SprintId),
                cancellationToken);
        }
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return TaskOperationResult.Success(task.Id);
    }

    public async Task<TaskOperationResult> UpdateAsync(
        UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string? fieldError = ValidateFields(
            request.Title, request.Description, request.Priority,
            request.AssignedUserId, request.UpdatedByUserId);
        if (fieldError is not null)
            return TaskOperationResult.Failure(fieldError);

        await using var transaction = await BeginMutationTransactionAsync(cancellationToken);
        string? contextError = await ValidateMutationContextAsync(
            request.ProjectId, request.SprintId, request.SprintBacklogItemId,
            cancellationToken);
        if (contextError is not null)
            return TaskOperationResult.Failure(contextError);

        var task = await GetScopedTasks(request.ProjectId, request.SprintId)
            .AsTracking()
            .FirstOrDefaultAsync(task => task.Id == request.Id &&
                task.SprintBacklogItemId == request.SprintBacklogItemId, cancellationToken);
        if (task is null)
            return TaskOperationResult.Failure("Task was not found in this sprint backlog item.");

        string? assignedUserId = NormalizeAssignee(request.AssignedUserId);
        bool assigneeChanged = !string.Equals(
            assignedUserId,
            task.AssignedUserId,
            StringComparison.Ordinal);

        if (assigneeChanged)
        {
            var assignmentResult = await _taskAssignmentService.ValidateForTaskAsync(
                task.Id, assignedUserId, cancellationToken);
            if (!assignmentResult.Succeeded || assignmentResult.ProjectId != request.ProjectId)
                return TaskOperationResult.Failure(
                    assignmentResult.ErrorMessage ?? "The selected assignee is not eligible for this project.");

            if (assignedUserId is not null &&
                !await _context.Users.AsNoTracking().AnyAsync(
                    user => user.Id == assignedUserId && !user.IsDisabled, cancellationToken))
                return TaskOperationResult.Failure("The selected assignee was not found or is disabled.");
        }

        string? previousAssignee = task.AssignedUserId;
        task.Title = request.Title.Trim();
        task.Description = request.Description.Trim();
        task.Priority = request.Priority;
        task.AssignedUserId = assignedUserId;
        task.Deadline = request.Deadline;
        task.UpdatedByUserId = request.UpdatedByUserId.Trim();
        task.UpdatedAt = _clock.UtcNow;

        if (assignedUserId is not null &&
            !string.Equals(previousAssignee, assignedUserId, StringComparison.Ordinal))
        {
            await NotifyUserAsync(
                assignedUserId,
                request.ProjectId,
                NotificationType.TaskAssigned,
                "Task assigned",
                $"You were assigned the task “{task.Title}”.",
                TaskDetailsUrl(request.ProjectId, request.SprintId, task.Id),
                cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return TaskOperationResult.Success(task.Id);
    }

    public async Task<TaskStatusTransitionResult> TransitionStatusAsync(
        TaskStatusTransitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ProjectId <= 0 || request.SprintId <= 0 || request.TaskId <= 0 ||
            string.IsNullOrWhiteSpace(request.ActorUserId) || request.ActorUserId.Trim().Length > 450 ||
            !Enum.IsDefined(request.Actor) || !Enum.IsDefined(request.CurrentStatus) ||
            !Enum.IsDefined(request.TargetStatus))
        {
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.Invalid, "The status request is invalid.");
        }

        await using var transaction = await BeginMutationTransactionAsync(cancellationToken);

        var project = await _context.Projects.AsNoTracking()
            .Where(item => item.Id == request.ProjectId)
            .Select(item => new { item.Methodology, item.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.NotFound, "Project was not found.");
        if (project.Methodology != ProjectMethodology.Scrum)
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.Invalid, "The Scrum Board is available only for Scrum projects.");
        if (project.Status == ProjectStatus.Archived)
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.ReadOnly, "Archived projects are read-only.");

        var sprint = await _context.Sprints.AsNoTracking()
            .Where(item => item.Id == request.SprintId && item.ProjectId == request.ProjectId)
            .Select(item => new { item.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (sprint is null)
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.NotFound, "Sprint was not found in this project.");
        if (sprint.Status == SprintStatus.Completed)
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.ReadOnly, "Completed sprints are read-only.");

        string actorUserId = request.ActorUserId.Trim();
        if (!await IsAuthorizedTransitionActorAsync(
                request.ProjectId, actorUserId, request.Actor, cancellationToken))
        {
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.Forbidden,
                "You are not authorized to change task status in this project.");
        }

        TaskItem? task = await _context.TaskItems
            .Where(item =>
                item.Id == request.TaskId &&
                item.SprintBacklogItem.SprintId == request.SprintId &&
                item.SprintBacklogItem.Sprint.ProjectId == request.ProjectId &&
                item.SprintBacklogItem.Sprint.Project.Methodology == ProjectMethodology.Scrum &&
                item.SprintBacklogItem.BacklogItem.ProjectId == request.ProjectId &&
                item.SprintBacklogItem.BacklogItem.Status == BacklogItemStatus.InSprint)
            .FirstOrDefaultAsync(cancellationToken);
        if (task is null)
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.NotFound, "Task was not found in this sprint.");

        if (request.Actor == TaskTransitionActor.Developer &&
            !string.Equals(task.AssignedUserId, actorUserId, StringComparison.Ordinal))
        {
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.Forbidden,
                "Developers can change only tasks assigned to them.");
        }

        if (task.Status != request.CurrentStatus)
        {
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.Conflict,
                "This task changed before your request was completed. Refresh the board and try again.");
        }

        if (!IsAllowedPhase6BTransition(request.CurrentStatus, request.TargetStatus))
        {
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.Invalid,
                "That status transition is not allowed. Done is reserved for a future QA pass.");
        }

        task.Status = request.TargetStatus;
        task.UpdatedByUserId = actorUserId;
        task.UpdatedAt = _clock.UtcNow;

        if (request.CurrentStatus == TaskItemStatus.InProgress &&
            request.TargetStatus == TaskItemStatus.InReview)
        {
            await NotifyProjectRoleAsync(
                request.ProjectId,
                ProjectMemberRole.QaTester,
                actorUserId,
                NotificationType.TaskSentToQa,
                "Task ready for QA",
                $"The task “{task.Title}” was submitted for QA review.",
                TaskDetailsUrl(request.ProjectId, request.SprintId, task.Id),
                cancellationToken);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return TaskStatusTransitionResult.Reject(
                TaskStatusTransitionFailure.Conflict,
                "This task was updated by someone else. Refresh the board and try again.");
        }

        return TaskStatusTransitionResult.Success(task.Status);
    }

    public async Task<QaReviewOperationResult> ReviewAsync(
        QaReviewTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string reviewerUserId = request.ReviewerUserId?.Trim() ?? string.Empty;
        string? notes = string.IsNullOrWhiteSpace(request.Notes)
            ? null
            : request.Notes.Trim();
        if (request.ProjectId <= 0 || request.SprintId <= 0 || request.TaskId <= 0 ||
            reviewerUserId.Length == 0 || reviewerUserId.Length > 450 ||
            !Enum.IsDefined(request.Result) || notes?.Length > 3000)
        {
            return QaReviewOperationResult.Reject(
                QaReviewOperationFailure.Invalid, "The QA review request is invalid.");
        }
        if (request.Result == QaReviewResult.Fail && notes is null)
        {
            return QaReviewOperationResult.Reject(
                QaReviewOperationFailure.Invalid, "A failure note is required when QA fails a task.");
        }

        if (request.EvidenceFiles.Count > 10)
            return QaReviewOperationResult.Reject(QaReviewOperationFailure.Invalid,
                "A QA review can include up to 10 evidence files.");
        var preparedEvidence = new List<(string Name, string Extension, byte[] Content)>();
        foreach (var upload in request.EvidenceFiles)
        {
            var prepared = await PrepareQaEvidenceAsync(upload, cancellationToken);
            if (prepared.Error is not null)
                return QaReviewOperationResult.Reject(QaReviewOperationFailure.Invalid, prepared.Error);
            preparedEvidence.Add(prepared.File!.Value);
        }

        await using var transaction = await BeginMutationTransactionAsync(cancellationToken);

        var project = await _context.Projects.AsNoTracking()
            .Where(item => item.Id == request.ProjectId)
            .Select(item => new { item.Methodology, item.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return QaReviewOperationResult.Reject(
                QaReviewOperationFailure.NotFound, "Project was not found.");
        if (project.Methodology != ProjectMethodology.Scrum)
            return QaReviewOperationResult.Reject(
                QaReviewOperationFailure.Invalid, "QA review is available only for Scrum tasks.");
        if (project.Status == ProjectStatus.Archived)
            return QaReviewOperationResult.Reject(
                QaReviewOperationFailure.ReadOnly, "Archived projects are read-only.");

        var sprint = await _context.Sprints.AsNoTracking()
            .Where(item => item.Id == request.SprintId && item.ProjectId == request.ProjectId)
            .Select(item => new { item.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (sprint is null)
            return QaReviewOperationResult.Reject(
                QaReviewOperationFailure.NotFound, "Sprint was not found in this project.");
        if (sprint.Status == SprintStatus.Completed)
            return QaReviewOperationResult.Reject(
                QaReviewOperationFailure.ReadOnly, "Completed sprints are read-only.");

        bool isProjectQa = await (
            from member in _context.ProjectMembers.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on member.UserId equals user.Id
            where member.ProjectId == request.ProjectId &&
                  member.UserId == reviewerUserId &&
                  member.Role == ProjectMemberRole.QaTester
            select member.Id)
            .AnyAsync(cancellationToken);
        if (!isProjectQa)
        {
            return QaReviewOperationResult.Reject(
                QaReviewOperationFailure.Forbidden,
                "Only a QA Tester assigned to this project can review the task.");
        }

        TaskItem? task = await _context.TaskItems
            .Include(item => item.SprintBacklogItem)
                .ThenInclude(item => item.BacklogItem)
            .Where(item =>
                item.Id == request.TaskId &&
                item.SprintBacklogItem.SprintId == request.SprintId &&
                item.SprintBacklogItem.Sprint.ProjectId == request.ProjectId &&
                item.SprintBacklogItem.Sprint.Project.Methodology == ProjectMethodology.Scrum &&
                item.SprintBacklogItem.BacklogItem.ProjectId == request.ProjectId)
            .FirstOrDefaultAsync(cancellationToken);
        if (task is null)
            return QaReviewOperationResult.Reject(
                QaReviewOperationFailure.NotFound, "Task was not found in this sprint.");
        if (task.Status != TaskItemStatus.InReview)
        {
            return QaReviewOperationResult.Reject(
                QaReviewOperationFailure.Conflict,
                "This task is no longer waiting for QA review. Refresh and try again.");
        }

        var review = new QaReview
        {
            TaskItemId = task.Id,
            QaUserId = reviewerUserId,
            Result = request.Result,
            Notes = notes,
            TestedAt = _clock.UtcNow
        };
        _context.QaReviews.Add(review);

        var storedEvidenceKeys = new List<string>();
        if (preparedEvidence.Count > 0 && _qaEvidenceStorage is null)
            return QaReviewOperationResult.Reject(QaReviewOperationFailure.Invalid,
                "QA evidence storage is unavailable.");
        try
        {
            foreach (var file in preparedEvidence)
            {
                await using var content = new MemoryStream(file.Content, writable: false);
                string key = await _qaEvidenceStorage!.SaveAsync(content, file.Extension, cancellationToken);
                storedEvidenceKeys.Add(key);
                review.EvidenceFiles.Add(new QaEvidence
                {
                    OriginalFileName = file.Name,
                    StorageKey = key,
                    FileSizeBytes = file.Content.LongLength,
                    Extension = file.Extension,
                    UploadedByUserId = reviewerUserId,
                    UploadedAt = _clock.UtcNow
                });
            }
        }
        catch
        {
            await CleanupQaEvidenceAsync(storedEvidenceKeys);
            _context.Entry(review).State = EntityState.Detached;
            return QaReviewOperationResult.Reject(QaReviewOperationFailure.Invalid,
                "QA evidence could not be stored.");
        }

        BacklogItem backlogItem = task.SprintBacklogItem.BacklogItem;
        if (request.Result == QaReviewResult.Pass)
        {
            task.Status = TaskItemStatus.Done;
            List<TaskItem> relatedTasks = await _context.TaskItems
                .Where(item => item.SprintBacklogItem.BacklogItemId == backlogItem.Id)
                .ToListAsync(cancellationToken);
            backlogItem.Status = relatedTasks.Count > 0 &&
                                 relatedTasks.All(item => item.Status == TaskItemStatus.Done)
                ? BacklogItemStatus.Completed
                : BacklogItemStatus.InSprint;
        }
        else
        {
            task.Status = TaskItemStatus.InProgress;
            backlogItem.Status = BacklogItemStatus.InSprint;
        }

        task.UpdatedByUserId = reviewerUserId;
        task.UpdatedAt = _clock.UtcNow;
        backlogItem.UpdatedByUserId = reviewerUserId;
        backlogItem.UpdatedAt = _clock.UtcNow;

        string taskUrl = TaskDetailsUrl(request.ProjectId, request.SprintId, task.Id);
        if (request.Result == QaReviewResult.Fail)
        {
            if (task.AssignedUserId is not null)
            {
                await NotifyUserAsync(
                    task.AssignedUserId,
                    request.ProjectId,
                    NotificationType.QaFailed,
                    "Task returned for changes",
                    $"The task “{task.Title}” was returned for changes after QA review.",
                    taskUrl,
                    cancellationToken);
            }
        }
        else
        {
            if (task.AssignedUserId is not null)
            {
                await NotifyUserAsync(
                    task.AssignedUserId,
                    request.ProjectId,
                    NotificationType.QaPassed,
                    "Task passed QA",
                    $"The task “{task.Title}” passed QA review.",
                    taskUrl,
                    cancellationToken);
            }
            await NotifyProjectRoleAsync(
                request.ProjectId,
                ProjectMemberRole.ProjectManager,
                reviewerUserId,
                NotificationType.QaPassed,
                "Task passed QA",
                $"The task “{task.Title}” passed QA review.",
                taskUrl,
                cancellationToken);
            await NotifyProjectRoleAsync(
                request.ProjectId,
                ProjectMemberRole.ScrumMaster,
                reviewerUserId,
                NotificationType.QaPassed,
                "Task passed QA",
                $"The task “{task.Title}” passed QA review.",
                taskUrl,
                cancellationToken);
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await CleanupQaEvidenceAsync(storedEvidenceKeys);
            return QaReviewOperationResult.Reject(
                QaReviewOperationFailure.Conflict,
                "This task was reviewed by someone else. Refresh and try again.");
        }
        catch
        {
            await CleanupQaEvidenceAsync(storedEvidenceKeys);
            throw;
        }

        return QaReviewOperationResult.Success(review.Id, task.Status, backlogItem.Status);
    }

    private static async Task<((string Name, string Extension, byte[] Content)? File, string? Error)> PrepareQaEvidenceAsync(
        QaEvidenceUpload upload, CancellationToken cancellationToken)
    {
        const int maximumBytes = 10 * 1024 * 1024;
        if (upload.Content == Stream.Null || upload.FileSizeBytes <= 0 || upload.FileSizeBytes > maximumBytes)
            return (null, "QA evidence files must be between 1 byte and 10 MB.");
        string name = Path.GetFileName((upload.OriginalFileName ?? string.Empty).Replace('\\', '/')).Trim();
        if (name.Length is 0 or > 255) return (null, "The QA evidence file name is invalid.");
        string extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".mp4" or ".pdf" or ".txt" or ".log"))
            return (null, "Allowed evidence formats are PNG, JPG, MP4, PDF, TXT and LOG.");
        string mime = upload.ContentType.Trim().ToLowerInvariant();
        bool mimeMatches = extension switch
        {
            ".png" => mime == "image/png",
            ".jpg" or ".jpeg" => mime == "image/jpeg",
            ".mp4" => mime == "video/mp4",
            ".pdf" => mime == "application/pdf",
            ".txt" or ".log" => mime == "text/plain",
            _ => false
        };
        if (!mimeMatches) return (null, "The evidence MIME type does not match its extension.");
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[81920];
        while (true)
        {
            int read = await upload.Content.ReadAsync(chunk, cancellationToken);
            if (read == 0) break;
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            if (buffer.Length > maximumBytes) return (null, "QA evidence files must be 10 MB or smaller.");
        }
        byte[] bytes = buffer.ToArray();
        if (bytes.LongLength != upload.FileSizeBytes || !IsValidEvidence(extension, bytes))
            return (null, "The evidence content does not match its file type or is incomplete.");
        return ((name, extension, bytes), null);
    }

    private static bool IsValidEvidence(string extension, byte[] bytes) => extension switch
    {
        ".png" => bytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        ".jpg" or ".jpeg" => bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255,
        ".pdf" => bytes.AsSpan().StartsWith("%PDF-"u8),
        ".mp4" => bytes.Length >= 12 && bytes.AsSpan(4, 4).SequenceEqual("ftyp"u8),
        ".txt" or ".log" => IsUtf8Text(bytes),
        _ => false
    };

    private static bool IsUtf8Text(byte[] bytes)
    {
        try { _ = new UTF8Encoding(false, true).GetString(bytes); return true; }
        catch (DecoderFallbackException) { return false; }
    }

    private async Task CleanupQaEvidenceAsync(IEnumerable<string> keys)
    {
        if (_qaEvidenceStorage is null) return;
        foreach (string key in keys)
        {
            try { await _qaEvidenceStorage.DeleteAsync(key, CancellationToken.None); }
            catch { /* Preserve the primary review result; storage cleanup is best effort. */ }
        }
    }

    private async Task<bool> IsAuthorizedTransitionActorAsync(
        int projectId,
        string actorUserId,
        TaskTransitionActor actor,
        CancellationToken cancellationToken)
    {
        if (actor == TaskTransitionActor.Admin)
            return true;

        ProjectMemberRole requiredRole = actor switch
        {
            TaskTransitionActor.ProjectManager => ProjectMemberRole.ProjectManager,
            TaskTransitionActor.ScrumMaster => ProjectMemberRole.ScrumMaster,
            TaskTransitionActor.Developer => ProjectMemberRole.Developer,
            _ => (ProjectMemberRole)0
        };

        return requiredRole != 0 && await _context.ProjectMembers.AsNoTracking()
            .AnyAsync(member =>
                member.ProjectId == projectId &&
                member.UserId == actorUserId &&
                member.Role == requiredRole,
                cancellationToken);
    }

    private static bool IsAllowedPhase6BTransition(
        TaskItemStatus currentStatus,
        TaskItemStatus targetStatus)
    {
        return (currentStatus == TaskItemStatus.ToDo && targetStatus == TaskItemStatus.InProgress) ||
               (currentStatus == TaskItemStatus.InProgress && targetStatus == TaskItemStatus.InReview);
    }

    private async Task NotifyUserAsync(
        string recipientUserId,
        int projectId,
        NotificationType type,
        string title,
        string message,
        string targetUrl,
        CancellationToken cancellationToken)
    {
        if (_notifications is null) return;
        await _notifications.EnqueueAsync(new CreateNotificationRequest
        {
            RecipientUserId = recipientUserId,
            ProjectId = projectId,
            Type = type,
            Title = title,
            Message = message,
            TargetUrl = targetUrl
        }, cancellationToken);
    }

    private async Task NotifyProjectRoleAsync(
        int projectId,
        ProjectMemberRole projectRole,
        string? excludedUserId,
        NotificationType type,
        string title,
        string message,
        string targetUrl,
        CancellationToken cancellationToken)
    {
        if (_notifications is null) return;
        await _notifications.EnqueueProjectRoleAsync(
            new CreateProjectRoleNotificationsRequest
            {
                ProjectId = projectId,
                ProjectRole = projectRole,
                ExcludedUserId = excludedUserId,
                Type = type,
                Title = title,
                Message = message,
                TargetUrl = targetUrl
            }, cancellationToken);
    }

    private static string TaskDetailsUrl(int projectId, int sprintId, int taskId) =>
        $"/Tasks/Details?projectId={projectId}&sprintId={sprintId}&id={taskId}";

    private static string TaskBoardUrl(int projectId, int sprintId) =>
        $"/Tasks/Board?projectId={projectId}&sprintId={sprintId}";

    private IQueryable<TaskItem> GetScopedTasks(int projectId, int sprintId)
    {
        return _context.TaskItems.AsNoTracking()
            .Where(task =>
                task.SprintBacklogItem.SprintId == sprintId &&
                task.SprintBacklogItem.Sprint.ProjectId == projectId &&
                task.SprintBacklogItem.Sprint.Project.Methodology == ProjectMethodology.Scrum &&
                task.SprintBacklogItem.BacklogItem.ProjectId == projectId);
    }

    private IQueryable<TaskDetails> GetTaskDetailsQuery(int projectId, int sprintId)
    {
        return GetScopedTasks(projectId, sprintId)
            .Select(task => new TaskDetails
            {
                Id = task.Id,
                ProjectId = task.SprintBacklogItem.Sprint.ProjectId,
                SprintId = task.SprintBacklogItem.SprintId,
                SprintBacklogItemId = task.SprintBacklogItemId,
                Title = task.Title,
                Description = task.Description,
                BacklogTitle = task.SprintBacklogItem.BacklogItem.Title,
                BacklogStatus = task.SprintBacklogItem.BacklogItem.Status,
                Priority = task.Priority,
                Status = task.Status,
                AssignedUserId = task.AssignedUserId,
                AssigneeName = _context.Users
                    .Where(user => user.Id == task.AssignedUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName
                        : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault(),
                Deadline = task.Deadline,
                CreatedAt = task.CreatedAt,
                UpdatedAt = task.UpdatedAt
            });
    }

    private async Task<string?> ValidateMutationContextAsync(
        int projectId,
        int sprintId,
        int sprintBacklogItemId,
        CancellationToken cancellationToken)
    {
        var project = await _context.Projects.AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new { project.Methodology, project.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (project is null)
            return "Project was not found.";
        if (project.Methodology != ProjectMethodology.Scrum)
            return "Tasks can only be managed for Scrum projects.";
        if (project.Status == ProjectStatus.Archived)
            return "Archived projects are read-only.";

        var sprint = await _context.Sprints.AsNoTracking()
            .Where(sprint => sprint.Id == sprintId && sprint.ProjectId == projectId)
            .Select(sprint => new { sprint.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (sprint is null)
            return "Sprint was not found in this project.";
        if (sprint.Status == SprintStatus.Completed)
            return "Completed sprints are read-only.";

        var backlog = await (
            from item in _context.SprintBacklogItems.AsNoTracking()
            join backlogItem in _context.BacklogItems.AsNoTracking()
                on item.BacklogItemId equals backlogItem.Id
            where item.Id == sprintBacklogItemId && item.SprintId == sprintId
            select new { backlogItem.ProjectId, backlogItem.Status })
            .FirstOrDefaultAsync(cancellationToken);
        if (backlog is null)
            return "Sprint backlog item was not found in this sprint.";
        if (backlog.ProjectId != projectId)
            return "Sprint and backlog item must belong to the same project.";
        if (backlog.Status != BacklogItemStatus.InSprint)
            return "Tasks can only be managed for backlog items that are in the sprint.";

        return null;
    }

    private static string? ValidateFields(
        string? title,
        string? description,
        PriorityLevel priority,
        string? assignedUserId,
        string? actorUserId)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            return "Title is required and must be 200 characters or fewer.";
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 3000)
            return "Description is required and must be 3000 characters or fewer.";
        if (!Enum.IsDefined(priority))
            return "Select a valid priority.";
        if (assignedUserId?.Trim().Length > 450)
            return "The selected assignee is invalid.";
        if (string.IsNullOrWhiteSpace(actorUserId) || actorUserId.Trim().Length > 450)
            return "An authenticated user is required.";

        return null;
    }

    private static string? NormalizeAssignee(string? assignedUserId)
        => string.IsNullOrWhiteSpace(assignedUserId) ? null : assignedUserId.Trim();

    private async Task<IDbContextTransaction?> BeginMutationTransactionAsync(
        CancellationToken cancellationToken)
    {
        // Keep state and assignment validation consistent with the eventual write.
        // In-memory providers used by the existing tests do not support transactions.
        if (!_context.Database.IsRelational() || _context.Database.CurrentTransaction is not null)
            return null;

        return await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
    }
}
