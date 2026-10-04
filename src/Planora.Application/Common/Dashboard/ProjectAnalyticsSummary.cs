namespace Planora.Application.Common.Dashboard;

public sealed class ProjectAnalyticsSummary
{
    public int ProjectId { get; set; }

    public string ProjectName { get; set; } =
        string.Empty;

    public decimal ProgressPercentage { get; set; }

    public int TotalTasks { get; set; }

    public int CompletedTasks { get; set; }

    public int ToDoTasks { get; set; }

    public int InProgressTasks { get; set; }

    public int InReviewTasks { get; set; }

    public int TotalIssues { get; set; }

    public int OpenIssues { get; set; }

    public int ResolvedIssues { get; set; }

    public IReadOnlyList<ProgressTrendPoint>
        ProgressHistory
    { get; set; }
        = Array.Empty<ProgressTrendPoint>();
}