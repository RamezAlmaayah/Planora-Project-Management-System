using Microsoft.EntityFrameworkCore;
using Planora.Application.Abstractions.Tasks;
using Planora.Application.Common.Tasks;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Tasks;

public sealed class TaskAssignmentService
    : ITaskAssignmentService
{
    private readonly ApplicationDbContext _context;

    public TaskAssignmentService(
        ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<TaskAssignmentValidationResult>
        ValidateForSprintBacklogItemAsync(
            int sprintBacklogItemId,
            string? assignedUserId,
            CancellationToken cancellationToken = default)
    {
        var assignmentContext =
            await (
                from sprintBacklogItem
                    in _context.Set<SprintBacklogItem>()
                        .AsNoTracking()

                join sprint
                    in _context.Set<Sprint>()
                        .AsNoTracking()
                    on sprintBacklogItem.SprintId
                    equals sprint.Id

                join backlogItem
                    in _context.Set<BacklogItem>()
                        .AsNoTracking()
                    on sprintBacklogItem.BacklogItemId
                    equals backlogItem.Id

                join project
                    in _context.Set<Project>()
                        .AsNoTracking()
                    on sprint.ProjectId
                    equals project.Id

                where sprintBacklogItem.Id
                    == sprintBacklogItemId

                select new
                {
                    SprintProjectId =
                        sprint.ProjectId,

                    BacklogProjectId =
                        backlogItem.ProjectId,

                    ProjectStatus =
                        project.Status
                })
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (assignmentContext is null)
        {
            return TaskAssignmentValidationResult
                .Failure(
                    "Sprint backlog item was not found.");
        }

        if (assignmentContext.SprintProjectId
            != assignmentContext.BacklogProjectId)
        {
            return TaskAssignmentValidationResult
                .Failure(
                    "Sprint and backlog item must belong to the same project.");
        }

        int projectId =
            assignmentContext.SprintProjectId;

        if (assignmentContext.ProjectStatus
            == ProjectStatus.Archived)
        {
            return TaskAssignmentValidationResult
                .Failure(
                    "Task assignment is disabled for archived projects.");
        }

        if (string.IsNullOrWhiteSpace(
                assignedUserId))
        {
            return TaskAssignmentValidationResult
                .Success(
                    projectId);
        }

        string normalizedUserId =
            assignedUserId.Trim();

        bool isProjectMember =
            await (
                from member in _context.Set<ProjectMember>().AsNoTracking()
                join user in _context.Users.AsNoTracking()
                    on member.UserId equals user.Id
                where member.ProjectId == projectId &&
                      member.UserId == normalizedUserId &&
                      !user.IsDisabled
                select member.Id)
                .AnyAsync(cancellationToken);

        if (!isProjectMember)
        {
            return TaskAssignmentValidationResult
                .Failure(
                    "The selected assignee must be an enabled member of this project.");
        }

        return TaskAssignmentValidationResult
            .Success(
                projectId);
    }

    public async Task<TaskAssignmentValidationResult>
        ValidateForTaskAsync(
            int taskItemId,
            string? assignedUserId,
            CancellationToken cancellationToken = default)
    {
        int? sprintBacklogItemId =
            await _context
                .Set<TaskItem>()
                .AsNoTracking()
                .Where(task =>
                    task.Id == taskItemId)
                .Select(task =>
                    (int?)task.SprintBacklogItemId)
                .FirstOrDefaultAsync(
                    cancellationToken);

        if (!sprintBacklogItemId.HasValue)
        {
            return TaskAssignmentValidationResult
                .Failure(
                    "Task was not found.");
        }

        return await
            ValidateForSprintBacklogItemAsync(
                sprintBacklogItemId.Value,
                assignedUserId,
                cancellationToken);
    }
}
