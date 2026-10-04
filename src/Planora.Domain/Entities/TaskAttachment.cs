namespace Planora.Domain.Entities;

public class TaskAttachment
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public string Extension { get; set; } = string.Empty;

    public string UploadedByUserId { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }

    public TaskItem TaskItem { get; set; } = null!;
}