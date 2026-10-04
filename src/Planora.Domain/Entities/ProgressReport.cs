namespace Planora.Domain.Entities;

public class ProgressReport
{
    public int Id { get; set; }

    public int ProjectId { get; set; }

    public decimal ProgressPercentage { get; set; }

    public string Summary { get; set; } = string.Empty;

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Project Project { get; set; } = null!;
}