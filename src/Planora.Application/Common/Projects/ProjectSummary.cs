using Planora.Domain.Enums;

namespace Planora.Application.Common.Projects;

public sealed class ProjectSummary
{
    public int Id { get; set; }

    public string Name { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public string Objectives { get; set; } =
        string.Empty;

    public string Scope { get; set; } =
        string.Empty;

    public ProjectMethodology Methodology { get; set; }

    public ProjectStatus Status { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public int MemberCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
