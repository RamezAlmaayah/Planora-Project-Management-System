using Planora.Domain.Enums;

namespace Planora.Domain.Entities;

public class Requirement
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public string Identifier { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public RequirementType Type { get; set; }

    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;

    public string? Rationale { get; set; }

    public string? Preconditions { get; set; }

    public string? ExceptionScenario { get; set; }

    public NfrCategory? NfrCategory { get; set; }

    public RequirementStatus Status { get; set; }
        = RequirementStatus.Draft;

    public string CreatedByUserId { get; set; } = string.Empty;

    public string? UpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;

    public ICollection<RequirementDependency> Dependencies { get; set; }
    = new List<RequirementDependency>();

    public ICollection<RequirementDependency> Dependents { get; set; }
        = new List<RequirementDependency>();

    public ICollection<RequirementTrace> Traces { get; set; }
    = new List<RequirementTrace>();

    public ICollection<DesignArtifactRequirement> DesignArtifacts { get; set; }
    = new List<DesignArtifactRequirement>();
}
