using Planora.Domain.Enums;

namespace Planora.Application.Common.Issues;

public sealed class IssueFilter
{
    public IssueStatus? Status { get; init; }
    public IssueSeverity? Severity { get; init; }
    public PriorityLevel? Priority { get; init; }
    public string? AssignedUserId { get; init; }
}

public class IssueSummary
{
    public int Id { get; init; }
    public int ProjectId { get; init; }
    public int? TaskItemId { get; init; }
    public int? TaskSprintId { get; init; }
    public string Title { get; init; } = string.Empty;
    public IssueSeverity Severity { get; init; }
    public PriorityLevel Priority { get; init; }
    public IssueStatus Status { get; init; }
    public string ReporterUserId { get; init; } = string.Empty;
    public string ReporterName { get; init; } = string.Empty;
    public string? AssignedUserId { get; init; }
    public string? AssigneeName { get; init; }
    public string? TaskTitle { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class IssueDetails : IssueSummary
{
    public string ProjectName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? ResolutionNotes { get; init; }
}

public sealed class IssueTaskOption
{
    public int TaskId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string SprintName { get; init; } = string.Empty;
}

public sealed class IssueAssigneeOption
{
    public string UserId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
}

public enum IssueOperationFailure
{
    None = 0,
    Validation,
    NotFound,
    Forbidden,
    ReadOnly,
    Conflict
}

public sealed class IssueOperationResult
{
    public bool Succeeded { get; init; }
    public int? IssueId { get; init; }
    public IssueStatus? Status { get; init; }
    public string? AssignedUserId { get; init; }
    public IssueOperationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;

    public static IssueOperationResult Success(
        int issueId,
        IssueStatus status,
        string? assignedUserId,
        string message) => new()
        {
            Succeeded = true,
            IssueId = issueId,
            Status = status,
            AssignedUserId = assignedUserId,
            Message = message
        };

    public static IssueOperationResult Failed(
        IssueOperationFailure failure,
        string message) => new()
        {
            Failure = failure,
            Message = message
        };
}

public sealed class CreateIssueRequest
{
    public int ProjectId { get; init; }
    public int? TaskItemId { get; init; }
    public int? VModelTestExecutionId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IssueSeverity Severity { get; init; }
    public PriorityLevel Priority { get; init; }
    public string ReporterUserId { get; init; } = string.Empty;
}

public sealed class UpdateIssueRequest
{
    public int ProjectId { get; init; }
    public int IssueId { get; init; }
    public IssueStatus ExpectedStatus { get; init; }
    public int? TaskItemId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IssueSeverity Severity { get; init; }
    public PriorityLevel Priority { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class AssignIssueRequest
{
    public int ProjectId { get; init; }
    public int IssueId { get; init; }
    public IssueStatus ExpectedStatus { get; init; }
    public string? ExpectedAssignedUserId { get; init; }
    public string AssignedUserId { get; init; } = string.Empty;
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class TransitionIssueRequest
{
    public int ProjectId { get; init; }
    public int IssueId { get; init; }
    public IssueStatus ExpectedStatus { get; init; }
    public IssueStatus TargetStatus { get; init; }
    public string? ResolutionNotes { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}
