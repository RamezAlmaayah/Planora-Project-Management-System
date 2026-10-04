using Planora.Domain.Enums;

namespace Planora.Application.Common.Tasks;

public sealed class QaReviewSummary
{
    public int Id { get; init; }
    public int TaskItemId { get; init; }
    public string ReviewerUserId { get; init; } = string.Empty;
    public string ReviewerName { get; init; } = string.Empty;
    public QaReviewResult Result { get; init; }
    public string? Notes { get; init; }
    public DateTime TestedAt { get; init; }
    public IReadOnlyList<QaEvidenceSummary> EvidenceFiles { get; init; } = Array.Empty<QaEvidenceSummary>();
}

public sealed class QaEvidenceSummary
{
    public int Id { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string Extension { get; init; } = string.Empty;
    public string UploadedByName { get; init; } = string.Empty;
    public string UploadedByUserId { get; init; } = string.Empty;
    public DateTime UploadedAt { get; init; }
}
