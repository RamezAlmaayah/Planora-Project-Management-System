using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Projects;

public sealed class ProjectDetailsViewModel
{
    public int Id { get; set; }

    public string Name { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public ProjectMethodology Methodology { get; set; }

    public ProjectStatus Status { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int MemberCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool CanManage { get; set; }
}