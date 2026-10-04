namespace Planora.Web.ViewModels.Projects;

public sealed class ProjectIndexViewModel
{
    public IReadOnlyList<ProjectListItemViewModel> Projects { get; set; }
        = Array.Empty<ProjectListItemViewModel>();

    public Planora.Domain.Enums.ProjectStatus? Status { get; set; }

    public string? Search { get; set; }

    public int TotalProjects =>
        Projects.Count;

    public int ScrumProjects =>
        Projects.Count(
            x => x.Methodology.ToString()
                .Equals(
                    "Scrum",
                    StringComparison.OrdinalIgnoreCase));

    public int VModelProjects =>
        Projects.Count(
            x => x.Methodology.ToString()
                .Equals(
                    "VModel",
                    StringComparison.OrdinalIgnoreCase));
}