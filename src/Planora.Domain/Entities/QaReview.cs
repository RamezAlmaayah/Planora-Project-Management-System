using Planora.Domain.Enums;

namespace Planora.Domain.Entities;

public class QaReview
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }

    public string QaUserId { get; set; } = string.Empty;

    public QaReviewResult Result { get; set; }

    public string? Notes { get; set; }

    public DateTime TestedAt { get; set; }

    public TaskItem TaskItem { get; set; } = null!;

    public ICollection<QaEvidence> EvidenceFiles { get; set; }
    = new List<QaEvidence>();
}