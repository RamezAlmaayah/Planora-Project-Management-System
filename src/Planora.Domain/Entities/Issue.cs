using Planora.Domain.Enums;

namespace Planora.Domain.Entities;

public class Issue
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public int? TaskItemId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public IssueSeverity Severity { get; set; } = IssueSeverity.Medium;

    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    public IssueStatus Status { get; set; } = IssueStatus.Open;

    public string ReporterUserId { get; set; } = string.Empty;

    public string? AssignedUserId { get; set; }

    public string? ResolutionNotes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;

    public TaskItem? TaskItem { get; set; }

    public ICollection<VModelTestExecutionIssue> VModelTestExecutions { get; set; } = [];
}
