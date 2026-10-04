namespace Planora.Domain.Entities;

public class QaEvidence
{
    public int Id { get; set; }

    public int QaReviewId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string Extension { get; set; } = string.Empty;

    public string UploadedByUserId { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }

    public QaReview QaReview { get; set; } = null!;
}