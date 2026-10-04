namespace Planora.Domain.Entities;

public class SrsDocument
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string StructuredContentJson { get; set; } = string.Empty;
    public int QualityScore { get; set; }
    public string QualityLevel { get; set; } = string.Empty;
    public string GeneratedByUserId { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public string SavedByUserId { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; }
    public Project Project { get; set; } = null!;
}
