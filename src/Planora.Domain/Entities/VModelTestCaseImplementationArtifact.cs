namespace Planora.Domain.Entities;
public class VModelTestCaseImplementationArtifact
{
    public int VModelTestCaseId { get; set; } public int ImplementationArtifactId { get; set; }
    public VModelTestCase VModelTestCase { get; set; } = null!;
    public ImplementationArtifact ImplementationArtifact { get; set; } = null!;
}
