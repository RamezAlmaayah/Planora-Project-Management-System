using Planora.Domain.Enums;

namespace Planora.Domain.Entities;

public class ImplementationArtifact
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ImplementationArtifactType Type { get; set; }
    public string? SourceReference { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public ICollection<ImplementationArtifactDesign> Designs { get; set; } = [];
    public ICollection<VModelTestCaseImplementationArtifact> TestCases { get; set; } = [];
}
