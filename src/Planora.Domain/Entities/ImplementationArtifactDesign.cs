namespace Planora.Domain.Entities;

public class ImplementationArtifactDesign
{
    public int ImplementationArtifactId { get; set; }
    public int DesignArtifactId { get; set; }

    public ImplementationArtifact ImplementationArtifact { get; set; } = null!;
    public DesignArtifact DesignArtifact { get; set; } = null!;
}
