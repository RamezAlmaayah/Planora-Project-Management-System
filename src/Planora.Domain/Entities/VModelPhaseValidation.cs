using Planora.Domain.Enums;
namespace Planora.Domain.Entities;
public class VModelPhaseValidation
{
    public int Id { get; set; } public int VModelPhaseId { get; set; }
    public VModelValidationResult Result { get; set; } public string Notes { get; set; } = string.Empty;
    public string ValidatedByUserId { get; set; } = string.Empty; public DateTime ValidatedAt { get; set; }
    public VModelPhase VModelPhase { get; set; } = null!;
}
