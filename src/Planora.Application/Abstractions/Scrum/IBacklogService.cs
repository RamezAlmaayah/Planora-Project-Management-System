using Planora.Application.Common.Scrum.Backlog;

namespace Planora.Application.Abstractions.Scrum;

public interface IBacklogService
{
    Task<IReadOnlyList<BacklogItemSummary>>
        GetProjectBacklogAsync(
            int projectId,
            CancellationToken cancellationToken = default);

    Task<BacklogItemSummary?>
        GetByIdAsync(
            int projectId,
            int backlogItemId,
            CancellationToken cancellationToken = default);

    Task<bool>
        BacklogItemExistsAsync(
            int projectId,
            int backlogItemId,
            CancellationToken cancellationToken = default);

    Task<BacklogOperationResult>
        CreateAsync(
            CreateBacklogItemRequest request,
            CancellationToken cancellationToken = default);

    Task<BacklogOperationResult>
        UpdateAsync(
            UpdateBacklogItemRequest request,
            CancellationToken cancellationToken = default);

    Task<BacklogOperationResult>
        DeleteAsync(
            int projectId,
            int backlogItemId,
            CancellationToken cancellationToken = default);
}
