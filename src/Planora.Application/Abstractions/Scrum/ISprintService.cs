using Planora.Application.Common.Scrum.Sprints;

namespace Planora.Application.Abstractions.Scrum;

public interface ISprintService
{
    Task<IReadOnlyList<SprintSummary>> GetProjectSprintsAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    Task<SprintSummary?> GetByIdAsync(
        int projectId,
        int sprintId,
        CancellationToken cancellationToken = default);

    Task<SprintOperationResult> CreateAsync(
        CreateSprintRequest request,
        CancellationToken cancellationToken = default);

    Task<SprintOperationResult> UpdateAsync(
        UpdateSprintRequest request,
        CancellationToken cancellationToken = default);

    Task<SprintOperationResult> StartAsync(
        int projectId,
        int sprintId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<SprintOperationResult> CompleteAsync(
        int projectId,
        int sprintId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SprintBacklogItemSummary>> GetSprintBacklogAsync(
        int projectId,
        int sprintId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SprintBacklogOption>> GetReadyBacklogAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    Task<SprintOperationResult> AddBacklogItemAsync(
        int projectId,
        int sprintId,
        int backlogItemId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<SprintOperationResult> RemoveBacklogItemAsync(
        int projectId,
        int sprintId,
        int backlogItemId,
        string userId,
        CancellationToken cancellationToken = default);
}

