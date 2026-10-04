using Planora.Domain.Enums;
namespace Planora.Domain.Entities;
public class VModelPhase
{
    public int Id { get; set; } public int ProjectId { get; set; }
    public VModelPhaseType PhaseType { get; set; } public int PhaseOrder { get; set; }
    public VModelPhaseStatus Status { get; set; } = VModelPhaseStatus.NotStarted;
    public DateTime? StartedAt { get; set; } public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } public DateTime? UpdatedAt { get; set; }
    public Project Project { get; set; } = null!;
    public ICollection<VModelPhaseValidation> Validations { get; set; } = [];
}
