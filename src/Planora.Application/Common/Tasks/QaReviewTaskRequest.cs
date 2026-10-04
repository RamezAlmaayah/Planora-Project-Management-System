using Planora.Domain.Enums;

namespace Planora.Application.Common.Tasks;

public sealed class QaReviewTaskRequest
{
    public int ProjectId { get; init; }
    public int SprintId { get; init; }
    public int TaskId { get; init; }
    public string ReviewerUserId { get; init; } = string.Empty;
    public QaReviewResult Result { get; init; }
    public string? Notes { get; init; }
    public IReadOnlyList<QaEvidenceUpload> EvidenceFiles { get; init; } = Array.Empty<QaEvidenceUpload>();
}

public sealed class QaEvidenceUpload
{
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public Stream Content { get; init; } = Stream.Null;
}
