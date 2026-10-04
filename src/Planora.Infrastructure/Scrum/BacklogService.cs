using Microsoft.EntityFrameworkCore;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Scrum;
using Planora.Application.Common.Scrum.Backlog;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Scrum;

public sealed class BacklogService : IBacklogService
{
    private readonly ApplicationDbContext _context;
    private readonly IClock _clock;

    public BacklogService(
        ApplicationDbContext context,
        IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IReadOnlyList<BacklogItemSummary>>
        GetProjectBacklogAsync(
            int projectId,
            CancellationToken cancellationToken = default)
    {
        return await _context.BacklogItems
            .AsNoTracking()
            .Where(item =>
                item.ProjectId == projectId)
            .OrderBy(item =>
                item.Status)
            .ThenBy(item =>
                item.Priority)
            .ThenByDescending(item =>
                item.CreatedAt)
            .Select(item =>
                new BacklogItemSummary
                {
                    Id =
                        item.Id,

                    ProjectId =
                        item.ProjectId,

                    Title =
                        item.Title,

                    Description =
                        item.Description,

                    Priority =
                        item.Priority,

                    Status =
                        item.Status,

                    CreatedAt =
                        item.CreatedAt,

                    UpdatedAt =
                        item.UpdatedAt,

                    IsAssignedToSprint =
                        item.SprintAssignments.Any()
                })
            .ToListAsync(
                cancellationToken);
    }

    public async Task<BacklogItemSummary?>
        GetByIdAsync(
            int projectId,
            int backlogItemId,
            CancellationToken cancellationToken = default)
    {
        return await _context.BacklogItems
            .AsNoTracking()
            .Where(item =>
                item.ProjectId == projectId
                && item.Id == backlogItemId)
            .Select(item =>
                new BacklogItemSummary
                {
                    Id =
                        item.Id,

                    ProjectId =
                        item.ProjectId,

                    Title =
                        item.Title,

                    Description =
                        item.Description,

                    Priority =
                        item.Priority,

                    Status =
                        item.Status,

                    CreatedAt =
                        item.CreatedAt,

                    UpdatedAt =
                        item.UpdatedAt,

                    IsAssignedToSprint =
                        item.SprintAssignments.Any()
                })
            .FirstOrDefaultAsync(
                cancellationToken);
    }

    public Task<bool>
        BacklogItemExistsAsync(
            int projectId,
            int backlogItemId,
            CancellationToken cancellationToken = default)
    {
        return _context.BacklogItems
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.ProjectId == projectId
                    && item.Id == backlogItemId,
                cancellationToken);
    }

    public async Task<BacklogOperationResult>
        CreateAsync(
            CreateBacklogItemRequest request,
            CancellationToken cancellationToken = default)
    {
        Project? project =
            await _context.Projects
                .FirstOrDefaultAsync(
                    project =>
                        project.Id == request.ProjectId,
                    cancellationToken);

        if (project is null)
        {
            return BacklogOperationResult.Failure(
                "Project was not found.");
        }

        if (project.Methodology !=
            ProjectMethodology.Scrum)
        {
            return BacklogOperationResult.Failure(
                "Backlog items can only be created for Scrum projects.");
        }

        if (project.Status ==
            ProjectStatus.Archived)
        {
            return BacklogOperationResult.Failure(
                "Archived projects cannot be modified.");
        }

        if (!IsManualStatus(
                request.Status))
        {
            return BacklogOperationResult.Failure(
                "Only Draft or Ready can be selected manually.");
        }

        string title =
            request.Title.Trim();

        string description =
            request.Description.Trim();

        if (string.IsNullOrWhiteSpace(
                title))
        {
            return BacklogOperationResult.Failure(
                "Backlog item title is required.");
        }

        if (string.IsNullOrWhiteSpace(
                description))
        {
            return BacklogOperationResult.Failure(
                "Backlog item description is required.");
        }

        if (!Enum.IsDefined(
                typeof(PriorityLevel),
                request.Priority))
        {
            return BacklogOperationResult.Failure(
                "Please select a valid priority.");
        }

        var item =
            new BacklogItem
            {
                ProjectId =
                    request.ProjectId,

                Title =
                    title,

                Description =
                    description,

                Priority =
                    request.Priority,

                Status =
                    request.Status,

                CreatedByUserId =
                    request.CreatedByUserId,

                CreatedAt =
                    _clock.UtcNow
            };

        _context.BacklogItems.Add(
            item);

        await _context.SaveChangesAsync(
            cancellationToken);

        return BacklogOperationResult.Success(
            ToSummary(
                item,
                false));
    }

    public async Task<BacklogOperationResult>
        UpdateAsync(
            UpdateBacklogItemRequest request,
            CancellationToken cancellationToken = default)
    {
        BacklogItem? item =
            await _context.BacklogItems
                .Include(item =>
                    item.Project)
                .Include(item =>
                    item.SprintAssignments)
                .FirstOrDefaultAsync(
                    item =>
                        item.Id == request.Id
                        && item.ProjectId == request.ProjectId,
                    cancellationToken);

        if (item is null)
        {
            return BacklogOperationResult.Failure(
                "Backlog item was not found.");
        }

        if (item.Project.Methodology !=
            ProjectMethodology.Scrum)
        {
            return BacklogOperationResult.Failure(
                "This action is available only for Scrum projects.");
        }

        if (item.Project.Status ==
            ProjectStatus.Archived)
        {
            return BacklogOperationResult.Failure(
                "Archived projects cannot be modified.");
        }

        if (item.Status ==
            BacklogItemStatus.Completed)
        {
            return BacklogOperationResult.Failure(
                "Completed backlog items are read-only.");
        }

        if (!IsManualStatus(
                request.Status))
        {
            return BacklogOperationResult.Failure(
                "In Sprint and Completed statuses are managed automatically.");
        }

        if (item.Status ==
            BacklogItemStatus.InSprint
            && request.Status !=
                BacklogItemStatus.InSprint)
        {
            return BacklogOperationResult.Failure(
                "An item assigned to a sprint cannot be moved manually.");
        }

        string title =
            request.Title.Trim();

        string description =
            request.Description.Trim();

        if (string.IsNullOrWhiteSpace(
                title))
        {
            return BacklogOperationResult.Failure(
                "Backlog item title is required.");
        }

        if (string.IsNullOrWhiteSpace(
                description))
        {
            return BacklogOperationResult.Failure(
                "Backlog item description is required.");
        }

        if (!Enum.IsDefined(
                typeof(PriorityLevel),
                request.Priority))
        {
            return BacklogOperationResult.Failure(
                "Please select a valid priority.");
        }

        item.Title =
            title;

        item.Description =
            description;

        item.Priority =
            request.Priority;

        item.Status =
            request.Status;

        item.UpdatedByUserId =
            request.UpdatedByUserId;

        item.UpdatedAt =
            _clock.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return BacklogOperationResult.Success(
            ToSummary(
                item,
                item.SprintAssignments.Count > 0));
    }

    public async Task<BacklogOperationResult>
        DeleteAsync(
            int projectId,
            int backlogItemId,
            CancellationToken cancellationToken = default)
    {
        BacklogItem? item =
            await _context.BacklogItems
                .Include(item =>
                    item.Project)
                .Include(item =>
                    item.SprintAssignments)
                .FirstOrDefaultAsync(
                    item =>
                        item.Id == backlogItemId
                        && item.ProjectId == projectId,
                    cancellationToken);

        if (item is null)
        {
            return BacklogOperationResult.Failure(
                "Backlog item was not found.");
        }

        if (item.Project.Methodology !=
            ProjectMethodology.Scrum)
        {
            return BacklogOperationResult.Failure(
                "This action is available only for Scrum projects.");
        }

        if (item.Project.Status ==
            ProjectStatus.Archived)
        {
            return BacklogOperationResult.Failure(
                "Archived projects cannot be modified.");
        }

        if (item.SprintAssignments.Count > 0
            || item.Status ==
                BacklogItemStatus.InSprint
            || item.Status ==
                BacklogItemStatus.Completed)
        {
            return BacklogOperationResult.Failure(
                "A backlog item that belongs to a sprint cannot be deleted.");
        }

        _context.BacklogItems.Remove(
            item);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new BacklogOperationResult
        {
            Succeeded = true
        };
    }

    private static bool IsManualStatus(
        BacklogItemStatus status)
    {
        return status ==
                BacklogItemStatus.Draft
            || status ==
                BacklogItemStatus.Ready;
    }

    private static BacklogItemSummary
        ToSummary(
            BacklogItem item,
            bool isAssignedToSprint)
    {
        return new BacklogItemSummary
        {
            Id =
                item.Id,

            ProjectId =
                item.ProjectId,

            Title =
                item.Title,

            Description =
                item.Description,

            Priority =
                item.Priority,

            Status =
                item.Status,

            CreatedAt =
                item.CreatedAt,

            UpdatedAt =
                item.UpdatedAt,

            IsAssignedToSprint =
                isAssignedToSprint
        };
    }
}
