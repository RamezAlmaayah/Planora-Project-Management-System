using Planora.Domain.Enums;

namespace Planora.Application.Common.Projects;

public sealed class CreateProjectRequest
{
    public string Name { get; set; } =
        string.Empty;

    public string Description { get; set; } =
        string.Empty;

    public ProjectMethodology Methodology { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string CreatedByUserId { get; set; } =
        string.Empty;

    public bool AddCreatorAsProjectManager { get; set; }
}