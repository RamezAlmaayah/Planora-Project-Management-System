using Microsoft.EntityFrameworkCore;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Dashboard;
using Planora.Application.Common.Dashboard;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Dashboard;

public sealed class ProjectAnalyticsService
    : IProjectAnalyticsService
{
    private readonly ApplicationDbContext _context;
    private readonly IClock _clock;

    public ProjectAnalyticsService(
        ApplicationDbContext context,
        IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<ProjectAnalyticsSummary?>
        GetProjectAnalyticsAsync(
            int projectId,
            CancellationToken cancellationToken = default)
    {
        var project =
            await _context
                .Set<Project>()
                .AsNoTracking()
                .Where(x =>
                    x.Id == projectId)
                .Select(x => new
                {
                    x.Id,
                    x.Name
                })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (project is null)
        {
            return null;
        }

        var taskQuery =
            from task in _context
                .Set<TaskItem>()
                .AsNoTracking()

            join sprintBacklogItem
                in _context
                    .Set<SprintBacklogItem>()
                    .AsNoTracking()
                on task.SprintBacklogItemId
                equals sprintBacklogItem.Id

            join sprint
                in _context
                    .Set<Sprint>()
                    .AsNoTracking()
                on sprintBacklogItem.SprintId
                equals sprint.Id

            where sprint.ProjectId ==
                projectId

            select task;

        int totalTasks =
            await taskQuery.CountAsync(
                cancellationToken);

        int completedTasks =
            await taskQuery.CountAsync(
                task =>
                    task.Status ==
                    TaskItemStatus.Done,
                cancellationToken);

        int toDoTasks =
            await taskQuery.CountAsync(
                task =>
                    task.Status ==
                    TaskItemStatus.ToDo,
                cancellationToken);

        int inProgressTasks =
            await taskQuery.CountAsync(
                task =>
                    task.Status ==
                    TaskItemStatus.InProgress,
                cancellationToken);

        int inReviewTasks =
            await taskQuery.CountAsync(
                task =>
                    task.Status ==
                    TaskItemStatus.InReview,
                cancellationToken);

        decimal progressPercentage =
            totalTasks == 0
                ? 0m
                : Math.Round(
                    (decimal)completedTasks
                    / totalTasks
                    * 100m,
                    2);

        var issueQuery =
            _context
                .Set<Issue>()
                .AsNoTracking()
                .Where(issue =>
                    issue.ProjectId ==
                    projectId);

        int totalIssues =
            await issueQuery.CountAsync(
                cancellationToken);

        int openIssues =
            await issueQuery.CountAsync(
                issue =>
                    issue.Status !=
                        IssueStatus.Resolved
                    &&
                    issue.Status !=
                        IssueStatus.Closed,
                cancellationToken);

        int resolvedIssues =
            await issueQuery.CountAsync(
                issue =>
                    issue.Status ==
                        IssueStatus.Resolved
                    ||
                    issue.Status ==
                        IssueStatus.Closed,
                cancellationToken);

        List<ProgressTrendPoint> history =
            await _context
                .Set<ProgressReport>()
                .AsNoTracking()
                .Where(report =>
                    report.ProjectId ==
                    projectId)
                .OrderByDescending(report =>
                    report.CreatedAt)
                .Take(12)
                .Select(report =>
                    new ProgressTrendPoint
                    {
                        RecordedAt =
                            report.CreatedAt,

                        ProgressPercentage =
                            report.ProgressPercentage
                    })
                .ToListAsync(
                    cancellationToken);

        history.Reverse();

        bool shouldAddCurrentPoint =
            history.Count == 0
            ||
            history[^1].ProgressPercentage
                != progressPercentage;

        if (shouldAddCurrentPoint)
        {
            history.Add(
                new ProgressTrendPoint
                {
                    RecordedAt =
                        _clock.UtcNow,

                    ProgressPercentage =
                        progressPercentage
                });
        }

        return new ProjectAnalyticsSummary
        {
            ProjectId =
                project.Id,

            ProjectName =
                project.Name,

            ProgressPercentage =
                progressPercentage,

            TotalTasks =
                totalTasks,

            CompletedTasks =
                completedTasks,

            ToDoTasks =
                toDoTasks,

            InProgressTasks =
                inProgressTasks,

            InReviewTasks =
                inReviewTasks,

            TotalIssues =
                totalIssues,

            OpenIssues =
                openIssues,

            ResolvedIssues =
                resolvedIssues,

            ProgressHistory =
                history
        };
    }
}