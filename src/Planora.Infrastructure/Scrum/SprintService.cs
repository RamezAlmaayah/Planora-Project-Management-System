using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Notifications;
using Planora.Application.Abstractions.Scrum;
using Planora.Application.Common.Notifications;
using Planora.Application.Common.Scrum.Sprints;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Scrum;

public sealed class SprintService : ISprintService
{
    private readonly ApplicationDbContext _context;
    private readonly IClock _clock;
    private readonly INotificationService? _notifications;

    public SprintService(
        ApplicationDbContext context,
        IClock clock,
        INotificationService? notifications = null)
    {
        _context = context;
        _clock = clock;
        _notifications = notifications;
    }

    public async Task<IReadOnlyList<SprintSummary>>
        GetProjectSprintsAsync(
            int projectId,
            CancellationToken cancellationToken = default)
    {
        return await _context.Sprints
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.StartDate)
            .Select(x => new SprintSummary
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                Name = x.Name,
                Goal = x.Goal,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Status = x.Status,
                BacklogItemCount = x.BacklogItems.Count,
                TaskCount = x.BacklogItems
                    .SelectMany(b => b.Tasks)
                    .Count(),
                CreatedAt = x.CreatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SprintSummary?>
        GetByIdAsync(
            int projectId,
            int sprintId,
            CancellationToken cancellationToken = default)
    {
        return await _context.Sprints
            .AsNoTracking()
            .Where(x =>
                x.Id == sprintId &&
                x.ProjectId == projectId)
            .Select(x => new SprintSummary
            {
                Id = x.Id,
                ProjectId = x.ProjectId,
                Name = x.Name,
                Goal = x.Goal,
                StartDate = x.StartDate,
                EndDate = x.EndDate,
                Status = x.Status,
                BacklogItemCount = x.BacklogItems.Count,
                TaskCount = x.BacklogItems
                    .SelectMany(b => b.Tasks)
                    .Count(),
                CreatedAt = x.CreatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SprintOperationResult>
        CreateAsync(
            CreateSprintRequest request,
            CancellationToken cancellationToken = default)
    {
        Project? project =
            await _context.Projects
                .FirstOrDefaultAsync(
                    x => x.Id == request.ProjectId,
                    cancellationToken);

        if (project is null)
            return SprintOperationResult.Failure(
                "Project was not found.");

        if (project.Methodology != ProjectMethodology.Scrum)
            return SprintOperationResult.Failure(
                "Sprints can only be created for Scrum projects.");

        if (project.Status == ProjectStatus.Archived)
            return SprintOperationResult.Failure(
                "Archived projects cannot be modified.");

        string name = request.Name.Trim();
        string goal = request.Goal.Trim();

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(goal))
        {
            return SprintOperationResult.Failure(
                "Please complete all required fields.");
        }

        if (request.EndDate.Date <= request.StartDate.Date)
        {
            return SprintOperationResult.Failure(
                "End date must be later than start date.");
        }

        var sprint = new Sprint
        {
            ProjectId = request.ProjectId,
            Name = name,
            Goal = goal,
            StartDate = request.StartDate.Date,
            EndDate = request.EndDate.Date,
            Status = SprintStatus.NotStarted,
            CreatedByUserId = request.CreatedByUserId,
            CreatedAt = _clock.UtcNow
        };

        _context.Sprints.Add(sprint);

        await _context.SaveChangesAsync(
            cancellationToken);

        return SprintOperationResult.Success(
            ToSummary(sprint, 0, 0));
    }

    public async Task<SprintOperationResult>
        UpdateAsync(
            UpdateSprintRequest request,
            CancellationToken cancellationToken = default)
    {
        Sprint? sprint =
            await _context.Sprints
                .Include(x => x.Project)
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.Id &&
                        x.ProjectId == request.ProjectId,
                    cancellationToken);

        if (sprint is null)
            return SprintOperationResult.Failure(
                "Sprint was not found.");

        if (sprint.Project.Status == ProjectStatus.Archived)
            return SprintOperationResult.Failure(
                "Archived projects cannot be modified.");

        if (sprint.Status == SprintStatus.Completed)
            return SprintOperationResult.Failure(
                "Completed sprints are read-only.");

        string name = request.Name.Trim();
        string goal = request.Goal.Trim();

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(goal))
        {
            return SprintOperationResult.Failure(
                "Please complete all required fields.");
        }

        if (request.EndDate.Date <= request.StartDate.Date)
        {
            return SprintOperationResult.Failure(
                "End date must be later than start date.");
        }

        sprint.Name = name;
        sprint.Goal = goal;
        sprint.StartDate = request.StartDate.Date;
        sprint.EndDate = request.EndDate.Date;
        sprint.UpdatedByUserId = request.UpdatedByUserId;
        sprint.UpdatedAt = _clock.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return SprintOperationResult.Success(
            await GetByIdAsync(
                request.ProjectId,
                request.Id,
                cancellationToken));
    }

    public async Task<SprintOperationResult>
        StartAsync(
            int projectId,
            int sprintId,
            string userId,
            CancellationToken cancellationToken = default)
    {
        Sprint? sprint =
            await GetEditableSprintAsync(
                projectId,
                sprintId,
                cancellationToken);

        if (sprint is null)
            return SprintOperationResult.Failure(
                "Sprint was not found.");

        if (sprint.Status != SprintStatus.NotStarted)
            return SprintOperationResult.Failure(
                "Only a Not Started sprint can be started.");

        sprint.Status = SprintStatus.InProgress;
        sprint.UpdatedByUserId = userId;
        sprint.UpdatedAt = _clock.UtcNow;

        await NotifyProjectMembersAsync(
            sprint.ProjectId,
            userId,
            NotificationType.SprintStarted,
            "Sprint started",
            $"The sprint “{sprint.Name}” has started.",
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);

        return SprintOperationResult.Success(
            await GetByIdAsync(
                projectId,
                sprintId,
                cancellationToken));
    }

    public async Task<SprintOperationResult>
        CompleteAsync(
            int projectId,
            int sprintId,
            string userId,
            CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction? transaction =
            await BeginCompletionTransactionAsync(cancellationToken);

        Sprint? sprint =
            await GetEditableSprintAsync(
                projectId,
                sprintId,
                cancellationToken);

        if (sprint is null)
            return SprintOperationResult.Failure(
                "Sprint was not found.");

        if (sprint.Status != SprintStatus.InProgress)
            return SprintOperationResult.Failure(
                "Only an In Progress sprint can be completed.");

        bool hasUnfinishedTasks = await _context.TaskItems
            .AsNoTracking()
            .AnyAsync(task =>
                task.SprintBacklogItem.SprintId == sprintId &&
                task.Status != TaskItemStatus.Done,
                cancellationToken);
        if (hasUnfinishedTasks)
            return SprintOperationResult.Failure(
                "All sprint tasks must be Done before completing the sprint.");

        sprint.Status = SprintStatus.Completed;
        sprint.UpdatedByUserId = userId;
        sprint.UpdatedAt = _clock.UtcNow;

        await NotifyProjectMembersAsync(
            sprint.ProjectId,
            userId,
            NotificationType.SprintCompleted,
            "Sprint completed",
            $"The sprint “{sprint.Name}” has been completed.",
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return SprintOperationResult.Success(
            await GetByIdAsync(
                projectId,
                sprintId,
                cancellationToken));
    }

    private async Task<IDbContextTransaction?> BeginCompletionTransactionAsync(
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational() ||
            _context.Database.CurrentTransaction is not null)
            return null;

        return await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
    }

    private async Task NotifyProjectMembersAsync(
        int projectId,
        string actorUserId,
        NotificationType type,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        if (_notifications is null) return;
        await _notifications.EnqueueProjectMembersAsync(
            new CreateProjectMemberNotificationsRequest
            {
                ProjectId = projectId,
                ExcludedUserId = actorUserId,
                Type = type,
                Title = title,
                Message = message,
                TargetUrl = $"/Sprints/Index?projectId={projectId}"
            }, cancellationToken);
    }

    public async Task<IReadOnlyList<SprintBacklogItemSummary>>
        GetSprintBacklogAsync(
            int projectId,
            int sprintId,
            CancellationToken cancellationToken = default)
    {
        return await _context.SprintBacklogItems
            .AsNoTracking()
            .Where(x =>
                x.SprintId == sprintId &&
                x.Sprint.ProjectId == projectId)
            .OrderBy(x => x.BacklogItem.Priority)
            .ThenBy(x => x.BacklogItem.Title)
            .Select(x => new SprintBacklogItemSummary
            {
                Id = x.BacklogItemId,
                SprintBacklogItemId = x.Id,
                Title = x.BacklogItem.Title,
                Priority = x.BacklogItem.Priority,
                Status = x.BacklogItem.Status,
                TaskCount = x.Tasks.Count
            })
            .ToListAsync(cancellationToken);
    }
    public async Task<IReadOnlyList<SprintBacklogOption>>
        GetReadyBacklogAsync(
            int projectId,
            CancellationToken cancellationToken = default)
    {
        return await _context.BacklogItems
            .AsNoTracking()
            .Where(x =>
                x.ProjectId == projectId &&
                x.Status == BacklogItemStatus.Ready)
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Title)
            .Select(x => new SprintBacklogOption
            {
                Id = x.Id,
                Title = x.Title,
                Priority = x.Priority,
                Status = x.Status
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<SprintOperationResult>
        AddBacklogItemAsync(
            int projectId,
            int sprintId,
            int backlogItemId,
            string userId,
            CancellationToken cancellationToken = default)
    {
        Sprint? sprint =
            await _context.Sprints
                .Include(x => x.Project)
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == sprintId &&
                        x.ProjectId == projectId,
                    cancellationToken);

        if (sprint is null)
            return SprintOperationResult.Failure(
                "Sprint was not found.");

        if (sprint.Project.Status == ProjectStatus.Archived)
            return SprintOperationResult.Failure(
                "Archived projects cannot be modified.");

        if (sprint.Status == SprintStatus.Completed)
            return SprintOperationResult.Failure(
                "Completed sprints are read-only.");

        BacklogItem? backlog =
            await _context.BacklogItems
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == backlogItemId &&
                        x.ProjectId == projectId,
                    cancellationToken);

        if (backlog is null)
            return SprintOperationResult.Failure(
                "Backlog item was not found.");

        if (backlog.Status != BacklogItemStatus.Ready)
            return SprintOperationResult.Failure(
                "Only Ready backlog items can be added to a sprint.");

        bool duplicate =
            await _context.SprintBacklogItems
                .AnyAsync(
                    x =>
                        x.SprintId == sprintId &&
                        x.BacklogItemId == backlogItemId,
                    cancellationToken);

        if (duplicate)
            return SprintOperationResult.Failure(
                "This backlog item is already in the sprint.");

        _context.SprintBacklogItems.Add(
            new SprintBacklogItem
            {
                SprintId = sprintId,
                BacklogItemId = backlogItemId,
                AddedByUserId = userId,
                AddedAt = _clock.UtcNow
            });

        backlog.Status = BacklogItemStatus.InSprint;
        backlog.UpdatedByUserId = userId;
        backlog.UpdatedAt = _clock.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return SprintOperationResult.Success(
            await GetByIdAsync(
                projectId,
                sprintId,
                cancellationToken));
    }

    public async Task<SprintOperationResult>
        RemoveBacklogItemAsync(
            int projectId,
            int sprintId,
            int backlogItemId,
            string userId,
            CancellationToken cancellationToken = default)
    {
        SprintBacklogItem? assignment =
            await _context.SprintBacklogItems
                .Include(x => x.Sprint)
                    .ThenInclude(x => x.Project)
                .Include(x => x.BacklogItem)
                .Include(x => x.Tasks)
                .FirstOrDefaultAsync(
                    x =>
                        x.SprintId == sprintId &&
                        x.BacklogItemId == backlogItemId &&
                        x.Sprint.ProjectId == projectId,
                    cancellationToken);

        if (assignment is null)
            return SprintOperationResult.Failure(
                "Sprint backlog item was not found.");

        if (assignment.Sprint.Project.Status == ProjectStatus.Archived)
            return SprintOperationResult.Failure(
                "Archived projects are read-only.");

        if (assignment.Sprint.Status == SprintStatus.Completed)
            return SprintOperationResult.Failure(
                "Completed sprints are read-only.");

        if (assignment.BacklogItem.Status ==
            BacklogItemStatus.Completed)
        {
            return SprintOperationResult.Failure(
                "Completed backlog items cannot be removed from a sprint.");
        }

        if (assignment.Tasks.Count > 0)
            return SprintOperationResult.Failure(
                "A backlog item with existing tasks cannot be removed.");

        _context.SprintBacklogItems.Remove(assignment);

        assignment.BacklogItem.Status =
            BacklogItemStatus.Ready;

        assignment.BacklogItem.UpdatedByUserId =
            userId;

        assignment.BacklogItem.UpdatedAt =
            _clock.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return SprintOperationResult.Success(
            await GetByIdAsync(
                projectId,
                sprintId,
                cancellationToken));
    }

    private async Task<Sprint?>
        GetEditableSprintAsync(
            int projectId,
            int sprintId,
            CancellationToken cancellationToken)
    {
        return await _context.Sprints
            .Include(x => x.Project)
            .FirstOrDefaultAsync(
                x =>
                    x.Id == sprintId &&
                    x.ProjectId == projectId &&
                    x.Project.Methodology ==
                        ProjectMethodology.Scrum &&
                    x.Project.Status !=
                        ProjectStatus.Archived,
                cancellationToken);
    }

    private static SprintSummary ToSummary(
        Sprint sprint,
        int backlogCount,
        int taskCount)
    {
        return new SprintSummary
        {
            Id = sprint.Id,
            ProjectId = sprint.ProjectId,
            Name = sprint.Name,
            Goal = sprint.Goal,
            StartDate = sprint.StartDate,
            EndDate = sprint.EndDate,
            Status = sprint.Status,
            BacklogItemCount = backlogCount,
            TaskCount = taskCount,
            CreatedAt = sprint.CreatedAt
        };
    }
}

