using System.ComponentModel.DataAnnotations;
using Planora.Application.Common.Issues;
using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Issues;

public sealed class IssueIndexViewModel
{
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public bool IsReadOnly { get; init; }
    public string? Search { get; init; }
    public IssueStatus? Status { get; init; }
    public IssueSeverity? Severity { get; init; }
    public PriorityLevel? Priority { get; init; }
    public string? AssignedUserId { get; init; }
    public IReadOnlyList<IssueAssigneeOption> Assignees { get; init; } = [];
    public IReadOnlyList<IssueSummary> Issues { get; init; } = [];
}

public sealed class IssueFormViewModel
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public IssueStatus ExpectedStatus { get; set; } = IssueStatus.Open;

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, StringLength(3000)]
    public string Description { get; set; } = string.Empty;

    [EnumDataType(typeof(IssueSeverity))]
    public IssueSeverity Severity { get; set; } = IssueSeverity.Medium;

    [EnumDataType(typeof(PriorityLevel))]
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    public int? TaskItemId { get; set; }
    public int? VModelTestExecutionId { get; set; }
    public IReadOnlyList<IssueTaskOption> TaskOptions { get; set; } = [];
}

public sealed class IssueDetailsViewModel
{
    public IssueDetails Issue { get; init; } = new();
    public bool IsReadOnly { get; init; }
    public bool CanEdit { get; init; }
    public bool CanAssign { get; init; }
    public bool CanStart { get; init; }
    public bool CanResolve { get; init; }
    public bool CanClose { get; init; }
    public bool CanReopen { get; init; }
    public IReadOnlyList<IssueAssigneeOption> Assignees { get; init; } = [];
}

public sealed class AssignIssueViewModel
{
    public int ProjectId { get; set; }
    public int IssueId { get; set; }
    public IssueStatus ExpectedStatus { get; set; }
    public string? ExpectedAssignedUserId { get; set; }

    [Required]
    public string AssignedUserId { get; set; } = string.Empty;
}

public sealed class TransitionIssueViewModel
{
    public int ProjectId { get; set; }
    public int IssueId { get; set; }
    public IssueStatus ExpectedStatus { get; set; }
    public IssueStatus TargetStatus { get; set; }

    [StringLength(3000)]
    public string? ResolutionNotes { get; set; }
}
