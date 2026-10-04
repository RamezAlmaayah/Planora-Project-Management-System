using Planora.Application.Common.Dashboard;
using Planora.Application.Common.Projects;

namespace Planora.Web.ViewModels.Dashboard;

public sealed class DashboardViewModel
{
    public string UserName { get; set; } =
        string.Empty;

    public IReadOnlyList<ProjectSummary> Projects { get; set; }
        = Array.Empty<ProjectSummary>();

    public int? SelectedProjectId { get; set; }

    public ProjectAnalyticsSummary? Analytics { get; set; }

    public int TotalProjects =>
        Projects.Count;

    public int PlanningProjects =>
        Projects.Count(x =>
            x.Status.ToString() == "Planning");

    public int ActiveProjects =>
        Projects.Count(x =>
            x.Status.ToString() == "Active");

    public int CompletedProjects =>
        Projects.Count(x =>
            x.Status.ToString() == "Completed");

    public int ArchivedProjects =>
        Projects.Count(x =>
            x.Status.ToString() == "Archived");

    public int ScrumProjects =>
        Projects.Count(x =>
            x.Methodology.ToString() == "Scrum");

    public int VModelProjects =>
        Projects.Count(x =>
            x.Methodology.ToString() == "VModel");

    public IReadOnlyList<ProjectSummary> RecentProjects =>
        Projects
            .OrderByDescending(x => x.CreatedAt)
            .Take(5)
            .ToList();
}