using Planora.Application.Common.Tasks;

namespace Planora.Application.Abstractions.Tasks;

public interface ITaskAssignmentService
{
    Task<TaskAssignmentValidationResult>
        ValidateForSprintBacklogItemAsync(
            int sprintBacklogItemId,
            string? assignedUserId,
            CancellationToken cancellationToken = default);

    Task<TaskAssignmentValidationResult>
        ValidateForTaskAsync(
            int taskItemId,
            string? assignedUserId,
            CancellationToken cancellationToken = default);
}