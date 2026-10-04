namespace Planora.Domain.Entities;

public class DesignArtifactRequirement
{
    public int DesignArtifactId { get; set; }
    public int RequirementId { get; set; }

    public DesignArtifact DesignArtifact { get; set; } = null!;
    public Requirement Requirement { get; set; } = null!;
}
