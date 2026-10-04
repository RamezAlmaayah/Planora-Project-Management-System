using Planora.Domain.Enums;

namespace Planora.Domain.Entities;

public class DesignArtifact
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Identifier { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DesignArtifactType Type { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public ICollection<DesignArtifactRequirement> Requirements { get; set; } = [];
    public ICollection<ImplementationArtifactDesign> ImplementationArtifacts { get; set; } = [];
}
