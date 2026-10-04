using Planora.Application.Common.Tasks;

namespace Planora.Application.Abstractions.Tasks;

public interface ITaskService
{
    Task<TaskListResult> GetSprintTasksAsync(int projectId, int sprintId, int page = 1,
        string? search = null, CancellationToken cancellationToken = default);
    Task<TaskDetails?> GetByIdAsync(int projectId, int sprintId, int taskId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskSummary>> GetSprintBoardTasksAsync(int projectId, int sprintId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskBacklogOption>> GetBacklogOptionsAsync(int projectId, int sprintId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskAssigneeOption>> GetAssigneeOptionsAsync(int projectId,
        CancellationToken cancellationToken = default);
    Task<TaskOperationResult> CreateAsync(CreateTaskRequest request,
        CancellationToken cancellationToken = default);
    Task<TaskOperationResult> UpdateAsync(UpdateTaskRequest request,
        CancellationToken cancellationToken = default);
    Task<TaskStatusTransitionResult> TransitionStatusAsync(TaskStatusTransitionRequest request,
        CancellationToken cancellationToken = default);
    Task<QaReviewOperationResult> ReviewAsync(QaReviewTaskRequest request,
        CancellationToken cancellationToken = default);
}
