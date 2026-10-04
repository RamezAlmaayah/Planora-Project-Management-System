namespace Planora.Domain.Entities;

public class RequirementDependency
{
    public int Id { get; set; }

    public int RequirementId { get; set; }

    public int DependsOnRequirementId { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Requirement Requirement { get; set; } = null!;

    public Requirement DependsOnRequirement { get; set; } = null!;
}