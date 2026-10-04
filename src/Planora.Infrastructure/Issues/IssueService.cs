using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Issues;
using Planora.Application.Abstractions.Notifications;
using Planora.Application.Common.Issues;
using Planora.Application.Common.Notifications;
using Planora.Application.Common.Security;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Issues;

public sealed class IssueService : IIssueService
{
    private const int TitleMaximumLength = 200;
    private const int DescriptionMaximumLength = 3000;
    private const int ResolutionNotesMaximumLength = 3000;
    private readonly ApplicationDbContext _context;
    private readonly IClock _clock;
    private readonly INotificationService? _notifications;

    public IssueService(
        ApplicationDbContext context,
        IClock clock,
        INotificationService? notifications = null)
    {
        _context = context;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<IReadOnlyList<IssueSummary>> GetProjectIssuesAsync(
        int projectId,
        string? search = null,
        IssueFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        string normalizedSearch = search?.Trim() ?? string.Empty;
        if (normalizedSearch.Length > TitleMaximumLength)
            normalizedSearch = normalizedSearch[..TitleMaximumLength];

        IQueryable<Issue> query = _context.Issues.AsNoTracking()
            .Where(issue => issue.ProjectId == projectId &&
                (issue.TaskItemId == null ||
                 issue.TaskItem!.SprintBacklogItem.Sprint.ProjectId == projectId &&
                 issue.TaskItem.SprintBacklogItem.BacklogItem.ProjectId == projectId));
        if (normalizedSearch.Length > 0)
            query = query.Where(issue =>
                issue.Title.Contains(normalizedSearch) ||
                issue.Description.Contains(normalizedSearch));
        if (filter?.Status is not null)
            query = query.Where(issue => issue.Status == filter.Status);
        if (filter?.Severity is not null)
            query = query.Where(issue => issue.Severity == filter.Severity);
        if (filter?.Priority is not null)
            query = query.Where(issue => issue.Priority == filter.Priority);
        if (!string.IsNullOrWhiteSpace(filter?.AssignedUserId))
            query = query.Where(issue => issue.AssignedUserId == filter.AssignedUserId);

        return await query
            .OrderByDescending(issue => issue.UpdatedAt ?? issue.CreatedAt)
            .ThenByDescending(issue => issue.Id)
            .Select(issue => new IssueSummary
            {
                Id = issue.Id,
                ProjectId = issue.ProjectId,
                TaskItemId = issue.TaskItemId,
                TaskSprintId = issue.TaskItemId == null
                    ? null
                    : issue.TaskItem!.SprintBacklogItem.SprintId,
                Title = issue.Title,
                Severity = issue.Severity,
                Priority = issue.Priority,
                Status = issue.Status,
                ReporterUserId = issue.ReporterUserId,
                ReporterName = _context.Users
                    .Where(user => user.Id == issue.ReporterUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName
                        : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? issue.ReporterUserId,
                AssignedUserId = issue.AssignedUserId,
                AssigneeName = issue.AssignedUserId == null
                    ? null
                    : _context.Users
                        .Where(user => user.Id == issue.AssignedUserId)
                        .Select(user => user.FullName != string.Empty
                            ? user.FullName
                            : user.UserName ?? user.Email ?? user.Id)
                        .FirstOrDefault(),
                TaskTitle = issue.TaskItemId == null ? null : issue.TaskItem!.Title,
                CreatedAt = issue.CreatedAt,
                UpdatedAt = issue.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IssueDetails?> GetByIdAsync(
        int projectId,
        int issueId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Issues.AsNoTracking()
            .Where(issue => issue.Id == issueId && issue.ProjectId == projectId &&
                (issue.TaskItemId == null ||
                 issue.TaskItem!.SprintBacklogItem.Sprint.ProjectId == projectId &&
                 issue.TaskItem.SprintBacklogItem.BacklogItem.ProjectId == projectId))
            .Select(issue => new IssueDetails
            {
                Id = issue.Id,
                ProjectId = issue.ProjectId,
                ProjectName = issue.Project.Name,
                TaskItemId = issue.TaskItemId,
                TaskSprintId = issue.TaskItemId == null
                    ? null
                    : issue.TaskItem!.SprintBacklogItem.SprintId,
                TaskTitle = issue.TaskItemId == null ? null : issue.TaskItem!.Title,
                Title = issue.Title,
                Description = issue.Description,
                Severity = issue.Severity,
                Priority = issue.Priority,
                Status = issue.Status,
                ReporterUserId = issue.ReporterUserId,
                ReporterName = _context.Users
                    .Where(user => user.Id == issue.ReporterUserId)
                    .Select(user => user.FullName != string.Empty
                        ? user.FullName
                        : user.UserName ?? user.Email ?? user.Id)
                    .FirstOrDefault() ?? issue.ReporterUserId,
                AssignedUserId = issue.AssignedUserId,
                AssigneeName = issue.AssignedUserId == null
                    ? null
                    : _context.Users
                        .Where(user => user.Id == issue.AssignedUserId)
                        .Select(user => user.FullName != string.Empty
                            ? user.FullName
                            : user.UserName ?? user.Email ?? user.Id)
                        .FirstOrDefault(),
                ResolutionNotes = issue.ResolutionNotes,
                CreatedAt = issue.CreatedAt,
                UpdatedAt = issue.UpdatedAt
            })
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IssueTaskOption>> GetTaskOptionsAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TaskItems.AsNoTracking()
            .Where(task =>
                task.SprintBacklogItem.Sprint.ProjectId == projectId &&
                task.SprintBacklogItem.BacklogItem.ProjectId == projectId)
            .OrderBy(task => task.SprintBacklogItem.Sprint.Name)
            .ThenBy(task => task.Title)
            .Select(task => new IssueTaskOption
            {
                TaskId = task.Id,
                Title = task.Title,
                SprintName = task.SprintBacklogItem.Sprint.Name
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IssueAssigneeOption>> GetDeveloperOptionsAsync(
        int projectId,
        CancellationToken cancellationToken = default)
    {
        return await (
            from member in _context.ProjectMembers.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on member.UserId equals user.Id
            where member.ProjectId == projectId &&
                  member.Role == ProjectMemberRole.Developer &&
                  !user.IsDisabled
            orderby user.FullName, user.Id
            select new IssueAssigneeOption
            {
                UserId = user.Id,
                DisplayName = user.FullName != string.Empty
                    ? user.FullName
                    : user.UserName ?? user.Email ?? user.Id
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IssueOperationResult> CreateAsync(
        CreateIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        string? fieldError = ValidateContent(
            request.Title, request.Description, request.Severity, request.Priority);
        if (fieldError is not null)
            return Invalid(fieldError);

        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ReporterUserId, cancellationToken);
        if (authorization.Error is not null)
            return authorization.Error;
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return ReadOnly();
        if (!await IsValidTaskAsync(
                request.ProjectId, request.TaskItemId, cancellationToken))
            return Invalid("The selected task does not belong to this project.");

        VModelTestExecution? failedExecution = null;
        if (request.VModelTestExecutionId.HasValue)
        {
            failedExecution = await _context.VModelTestExecutions
                .Include(execution => execution.VModelTestCase)
                .SingleOrDefaultAsync(execution =>
                    execution.Id == request.VModelTestExecutionId.Value &&
                    execution.VModelTestCase.ProjectId == request.ProjectId &&
                    execution.Result == VModelTestResult.Fail,
                    cancellationToken);
            if (failedExecution is null)
                return Invalid("The failed test execution does not belong to this project.");
        }

        var issue = new Issue
        {
            ProjectId = request.ProjectId,
            TaskItemId = request.TaskItemId,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Severity = request.Severity,
            Priority = request.Priority,
            Status = IssueStatus.Open,
            ReporterUserId = request.ReporterUserId,
            CreatedAt = _clock.UtcNow
        };
        _context.Issues.Add(issue);
        if (failedExecution is not null)
        {
            _context.VModelTestExecutionIssues.Add(new VModelTestExecutionIssue
            {
                VModelTestExecution = failedExecution,
                Issue = issue
            });
            _context.ActivityLogs.Add(CreateAudit(
                "VModelTestIssueLinked", issue, request.ReporterUserId,
                "Issue linked to failed V-Model test execution.", null,
                new { TestExecutionId = failedExecution.Id }));
        }
        _context.ActivityLogs.Add(CreateAudit(
            "IssueCreated", issue, request.ReporterUserId,
            "Issue reported.", null,
            new { issue.Title, issue.Severity, issue.Priority, issue.TaskItemId }));
        await _context.SaveChangesAsync(cancellationToken);
        return Success(issue, "Issue reported successfully.");
    }

    public async Task<IssueOperationResult> UpdateAsync(
        UpdateIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        string? fieldError = ValidateContent(
            request.Title, request.Description, request.Severity, request.Priority);
        if (fieldError is not null)
            return Invalid(fieldError);
        if (!Enum.IsDefined(request.ExpectedStatus))
            return Invalid("The expected issue status is invalid.");

        Issue? issue = await FindIssueAsync(
            request.ProjectId, request.IssueId, cancellationToken);
        if (issue is null)
            return NotFound();
        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ActorUserId, cancellationToken);
        if (authorization.Error is not null)
            return authorization.Error;
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return ReadOnly();
        if (issue.Status != request.ExpectedStatus)
            return Conflict("The issue changed before your edit. Refresh and try again.");
        if (issue.Status is not (IssueStatus.Open or IssueStatus.Assigned))
            return ReadOnly("Issue content becomes read-only once work starts.");

        bool isManager = CanManage(authorization.Access!);
        bool isReporterEditingOpen = issue.Status == IssueStatus.Open &&
            issue.ReporterUserId == request.ActorUserId;
        if (!isManager && !isReporterEditingOpen)
            return Forbidden("You are not authorized to edit this issue.");
        if (!await IsValidTaskAsync(
                request.ProjectId, request.TaskItemId, cancellationToken))
            return Invalid("The selected task does not belong to this project.");

        var oldValues = new
        {
            issue.Title,
            issue.Severity,
            issue.Priority,
            issue.TaskItemId
        };
        issue.Title = request.Title.Trim();
        issue.Description = request.Description.Trim();
        issue.Severity = request.Severity;
        issue.Priority = request.Priority;
        issue.TaskItemId = request.TaskItemId;
        issue.UpdatedAt = _clock.UtcNow;
        _context.ActivityLogs.Add(CreateAudit(
            "IssueEdited", issue, request.ActorUserId,
            "Issue report updated.", oldValues,
            new { issue.Title, issue.Severity, issue.Priority, issue.TaskItemId }));
        await _context.SaveChangesAsync(cancellationToken);
        return Success(issue, "Issue updated successfully.");
    }

    public async Task<IssueOperationResult> AssignAsync(
        AssignIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.ExpectedStatus) ||
            string.IsNullOrWhiteSpace(request.AssignedUserId))
            return Invalid("The assignment request is invalid.");

        Issue? issue = await FindIssueAsync(
            request.ProjectId, request.IssueId, cancellationToken);
        if (issue is null)
            return NotFound();
        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ActorUserId, cancellationToken);
        if (authorization.Error is not null)
            return authorization.Error;
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return ReadOnly();
        if (!CanManage(authorization.Access!))
            return Forbidden("Only an authorized project manager or Scrum Master can assign issues.");
        if (issue.Status != request.ExpectedStatus ||
            !string.Equals(
                issue.AssignedUserId,
                request.ExpectedAssignedUserId,
                StringComparison.Ordinal))
            return Conflict("The issue assignment changed. Refresh and try again.");
        if (issue.Status is not (IssueStatus.Open or IssueStatus.Assigned or IssueStatus.Reopened))
            return Conflict("Issues cannot be reassigned in their current status.");
        if (issue.AssignedUserId == request.AssignedUserId)
            return Conflict("The issue is already assigned to this developer.");

        bool validAssignee = await (
            from member in _context.ProjectMembers.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on member.UserId equals user.Id
            where member.ProjectId == request.ProjectId &&
                  member.UserId == request.AssignedUserId &&
                  member.Role == ProjectMemberRole.Developer &&
                  !user.IsDisabled
            select user.Id)
            .AnyAsync(cancellationToken);
        if (!validAssignee)
            return Invalid("The assignee must be a Developer in this project.");

        string? previousAssignee = issue.AssignedUserId;
        IssueStatus previousStatus = issue.Status;
        issue.AssignedUserId = request.AssignedUserId;
        if (issue.Status == IssueStatus.Open)
            issue.Status = IssueStatus.Assigned;
        issue.UpdatedAt = _clock.UtcNow;
        _context.ActivityLogs.Add(CreateAudit(
            previousAssignee is null ? "IssueAssigned" : "IssueReassigned",
            issue,
            request.ActorUserId,
            previousAssignee is null ? "Issue assigned." : "Issue reassigned.",
            new { AssignedUserId = previousAssignee, Status = previousStatus },
            new { issue.AssignedUserId, issue.Status }));
        await NotifyUserAsync(
            request.AssignedUserId,
            request.ProjectId,
            NotificationType.IssueAssigned,
            "Issue assigned",
            $"You were assigned the issue “{issue.Title}”.",
            IssueDetailsUrl(request.ProjectId, issue.Id),
            cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return Success(issue, "Issue assignment updated.");
    }

    public async Task<IssueOperationResult> TransitionAsync(
        TransitionIssueRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.ExpectedStatus) ||
            !Enum.IsDefined(request.TargetStatus) ||
            request.ExpectedStatus == request.TargetStatus)
            return Invalid("The requested issue status transition is invalid.");
        if (!IsAllowedTransition(request.ExpectedStatus, request.TargetStatus))
            return Invalid("This issue status transition is not allowed.");
        if (request.ExpectedStatus == IssueStatus.Open &&
            request.TargetStatus == IssueStatus.Assigned)
            return Invalid("Assign a project Developer to move an Open issue to Assigned.");

        Issue? issue = await FindIssueAsync(
            request.ProjectId, request.IssueId, cancellationToken);
        if (issue is null)
            return NotFound();
        var authorization = await ValidateProjectAccessAsync(
            request.ProjectId, request.ActorUserId, cancellationToken);
        if (authorization.Error is not null)
            return authorization.Error;
        if (authorization.Project!.Status == ProjectStatus.Archived)
            return ReadOnly();
        if (issue.Status != request.ExpectedStatus)
            return Conflict("The issue status changed. Refresh and try again.");

        bool developerTransition = request.ExpectedStatus is
            IssueStatus.Assigned or IssueStatus.Reopened or IssueStatus.InProgress;
        if (developerTransition && !CanAssignedDeveloperTransition(
                authorization.Access!, issue, request.ActorUserId))
            return Forbidden("Only the assigned project Developer can perform this transition.");

        bool qaTransition = request.ExpectedStatus is IssueStatus.Resolved or IssueStatus.Closed;
        if (qaTransition && !CanQaTransition(authorization.Access!))
            return Forbidden("Only a QA Tester in this project can verify this issue.");

        string? notes = request.ResolutionNotes?.Trim();
        if (request.TargetStatus == IssueStatus.Resolved)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return Invalid("Resolution notes are required before resolving an issue.");
            if (notes.Length > ResolutionNotesMaximumLength)
                return Invalid($"Resolution notes must be {ResolutionNotesMaximumLength} characters or fewer.");
            issue.ResolutionNotes = notes;
        }

        IssueStatus previousStatus = issue.Status;
        issue.Status = request.TargetStatus;
        issue.UpdatedAt = _clock.UtcNow;
        string action = request.TargetStatus switch
        {
            IssueStatus.Resolved => "IssueResolved",
            IssueStatus.Closed => "IssueClosed",
            IssueStatus.Reopened => "IssueReopened",
            _ => "IssueStatusChanged"
        };
        _context.ActivityLogs.Add(CreateAudit(
            action,
            issue,
            request.ActorUserId,
            $"Issue status changed from {previousStatus} to {issue.Status}.",
            new { Status = previousStatus },
            new { issue.Status }));

        string issueUrl = IssueDetailsUrl(request.ProjectId, issue.Id);
        if (request.TargetStatus == IssueStatus.Resolved)
        {
            await NotifyProjectRoleAsync(
                request.ProjectId,
                ProjectMemberRole.QaTester,
                request.ActorUserId,
                NotificationType.IssueResolved,
                "Issue ready for QA",
                $"The issue “{issue.Title}” was resolved and is ready for verification.",
                issueUrl,
                cancellationToken);
        }
        else if (request.TargetStatus == IssueStatus.Reopened &&
                 issue.AssignedUserId is not null)
        {
            await NotifyUserAsync(
                issue.AssignedUserId,
                request.ProjectId,
                NotificationType.IssueReopened,
                "Issue reopened",
                $"The issue “{issue.Title}” was reopened.",
                issueUrl,
                cancellationToken);
        }
        else if (request.TargetStatus == IssueStatus.Closed)
        {
            await NotifyUserAsync(
                issue.ReporterUserId,
                request.ProjectId,
                NotificationType.IssueClosed,
                "Issue closed",
                $"The issue “{issue.Title}” was verified and closed.",
                issueUrl,
                cancellationToken);
            if (issue.AssignedUserId is not null)
            {
                await NotifyUserAsync(
                    issue.AssignedUserId,
                    request.ProjectId,
                    NotificationType.IssueClosed,
                    "Issue closed",
                    $"The issue “{issue.Title}” was verified and closed.",
                    issueUrl,
                    cancellationToken);
            }
        }

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("The issue was updated by another request. Refresh and try again.");
        }
        return Success(issue, "Issue status updated.");
    }

    private async Task<Issue?> FindIssueAsync(
        int projectId,
        int issueId,
        CancellationToken cancellationToken)
    {
        if (projectId <= 0 || issueId <= 0)
            return null;
        return await _context.Issues
            .Where(issue => issue.Id == issueId && issue.ProjectId == projectId &&
                (issue.TaskItemId == null ||
                 issue.TaskItem!.SprintBacklogItem.Sprint.ProjectId == projectId &&
                 issue.TaskItem.SprintBacklogItem.BacklogItem.ProjectId == projectId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<(Project? Project, ActorAccess? Access, IssueOperationResult? Error)>
        ValidateProjectAccessAsync(
            int projectId,
            string actorUserId,
            CancellationToken cancellationToken)
    {
        if (projectId <= 0 || string.IsNullOrWhiteSpace(actorUserId))
            return (null, null, Invalid("The issue request is invalid."));
        Project? project = await _context.Projects.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == projectId, cancellationToken);
        if (project is null)
            return (null, null, IssueOperationResult.Failed(
                IssueOperationFailure.NotFound, "The project was not found."));

        List<string?> roles = await (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where userRole.UserId == actorUserId
            select role.Name)
            .ToListAsync(cancellationToken);
        if (!roles.Any(role => role is not null && SystemRoles.All.Contains(role)))
            return (null, null, Forbidden("You are not authorized to access issues."));

        ProjectMemberRole? projectRole = await _context.ProjectMembers.AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == actorUserId)
            .Select(member => (ProjectMemberRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);
        bool isAdmin = roles.Contains(SystemRoles.Admin, StringComparer.Ordinal);
        if (!isAdmin && projectRole is null)
            return (null, null, Forbidden("You are not a member of this project."));

        return (project, new ActorAccess(
            isAdmin,
            roles.Contains(SystemRoles.ProjectManager, StringComparer.Ordinal),
            roles.Contains(SystemRoles.ScrumMaster, StringComparer.Ordinal),
            roles.Contains(SystemRoles.Developer, StringComparer.Ordinal),
            roles.Contains(SystemRoles.QaTester, StringComparer.Ordinal),
            projectRole), null);
    }

    private async Task<bool> IsValidTaskAsync(
        int projectId,
        int? taskItemId,
        CancellationToken cancellationToken)
    {
        if (taskItemId is null)
            return true;
        return await _context.TaskItems.AsNoTracking()
            .AnyAsync(task =>
                task.Id == taskItemId &&
                task.SprintBacklogItem.Sprint.ProjectId == projectId &&
                task.SprintBacklogItem.BacklogItem.ProjectId == projectId,
                cancellationToken);
    }

    private static string? ValidateContent(
        string title,
        string description,
        IssueSeverity severity,
        PriorityLevel priority)
    {
        string normalizedTitle = title?.Trim() ?? string.Empty;
        string normalizedDescription = description?.Trim() ?? string.Empty;
        if (normalizedTitle.Length == 0)
            return "Issue title is required.";
        if (normalizedTitle.Length > TitleMaximumLength)
            return $"Issue title must be {TitleMaximumLength} characters or fewer.";
        if (normalizedDescription.Length == 0)
            return "Issue description is required.";
        if (normalizedDescription.Length > DescriptionMaximumLength)
            return $"Issue description must be {DescriptionMaximumLength} characters or fewer.";
        if (!Enum.IsDefined(severity) || !Enum.IsDefined(priority))
            return "Issue severity or priority is invalid.";
        return null;
    }

    private static bool IsAllowedTransition(IssueStatus source, IssueStatus target) =>
        source switch
        {
            IssueStatus.Open => target == IssueStatus.Assigned,
            IssueStatus.Assigned => target == IssueStatus.InProgress,
            IssueStatus.InProgress => target == IssueStatus.Resolved,
            IssueStatus.Resolved => target is IssueStatus.Closed or IssueStatus.Reopened,
            IssueStatus.Closed => target == IssueStatus.Reopened,
            IssueStatus.Reopened => target == IssueStatus.InProgress,
            _ => false
        };

    private static bool CanManage(ActorAccess access) =>
        access.IsAdmin ||
        access.HasProjectManagerRole && access.ProjectRole == ProjectMemberRole.ProjectManager ||
        access.HasScrumMasterRole && access.ProjectRole == ProjectMemberRole.ScrumMaster;

    private static bool CanAssignedDeveloperTransition(
        ActorAccess access,
        Issue issue,
        string actorUserId) =>
        access.HasDeveloperRole &&
        access.ProjectRole == ProjectMemberRole.Developer &&
        issue.AssignedUserId == actorUserId;

    private static bool CanQaTransition(ActorAccess access) =>
        access.HasQaTesterRole &&
        !access.IsAdmin &&
        !access.HasProjectManagerRole &&
        !access.HasScrumMasterRole &&
        !access.HasDeveloperRole &&
        access.ProjectRole == ProjectMemberRole.QaTester;

    private ActivityLog CreateAudit(
        string action,
        Issue issue,
        string actorUserId,
        string description,
        object? oldValues,
        object? newValues) => new()
        {
            ActorUserId = actorUserId,
            ProjectId = issue.ProjectId,
            Action = action,
            ResourceType = nameof(Issue),
            ResourceId = issue.Id.ToString(),
            Description = description,
            OldValues = oldValues is null ? null : JsonSerializer.Serialize(oldValues),
            NewValues = newValues is null ? null : JsonSerializer.Serialize(newValues),
            CreatedAt = _clock.UtcNow
        };

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

    private static string IssueDetailsUrl(int projectId, int issueId) =>
        $"/Issues/Details?projectId={projectId}&id={issueId}";

    private static IssueOperationResult Success(Issue issue, string message) =>
        IssueOperationResult.Success(
            issue.Id, issue.Status, issue.AssignedUserId, message);
    private static IssueOperationResult Invalid(string message) =>
        IssueOperationResult.Failed(IssueOperationFailure.Validation, message);
    private static IssueOperationResult NotFound() =>
        IssueOperationResult.Failed(
            IssueOperationFailure.NotFound, "The issue was not found in this project.");
    private static IssueOperationResult Forbidden(string message) =>
        IssueOperationResult.Failed(IssueOperationFailure.Forbidden, message);
    private static IssueOperationResult ReadOnly(
        string message = "Archived projects are read-only.") =>
        IssueOperationResult.Failed(IssueOperationFailure.ReadOnly, message);
    private static IssueOperationResult Conflict(string message) =>
        IssueOperationResult.Failed(IssueOperationFailure.Conflict, message);

    private sealed record ActorAccess(
        bool IsAdmin,
        bool HasProjectManagerRole,
        bool HasScrumMasterRole,
        bool HasDeveloperRole,
        bool HasQaTesterRole,
        ProjectMemberRole? ProjectRole);
}
