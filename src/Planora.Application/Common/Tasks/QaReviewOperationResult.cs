using Planora.Domain.Enums;

namespace Planora.Application.Common.Tasks;

public sealed class QaReviewOperationResult
{
    public bool Succeeded { get; init; }
    public int? ReviewId { get; init; }
    public TaskItemStatus? TaskStatus { get; init; }
    public BacklogItemStatus? BacklogStatus { get; init; }
    public QaReviewOperationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;

    public static QaReviewOperationResult Success(
        int reviewId,
        TaskItemStatus taskStatus,
        BacklogItemStatus backlogStatus) => new()
    {
        Succeeded = true,
        ReviewId = reviewId,
        TaskStatus = taskStatus,
        BacklogStatus = backlogStatus,
        Message = taskStatus == TaskItemStatus.Done
            ? "QA review passed. The task is Done."
            : "QA review failed. The task returned to In Progress."
    };

    public static QaReviewOperationResult Reject(
        QaReviewOperationFailure failure,
        string message) => new()
    {
        Failure = failure,
        Message = message
    };
}
