using Planora.Domain.Enums;

namespace Planora.Domain.Entities;

public class RequirementTrace
{
    public int Id { get; set; }

    public int RequirementId { get; set; }

    public RequirementTraceStage Stage { get; set; }

    public string ReferenceCode { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Requirement Requirement { get; set; } = null!;
}