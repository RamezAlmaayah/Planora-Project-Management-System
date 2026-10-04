namespace Planora.Application.Common.Tasks;

public sealed class TaskAttachmentOptions
{
    public const string SectionName = "TaskAttachments";
    public const long DefaultMaximumFileSizeBytes = 10 * 1024 * 1024;
    public long MaximumFileSizeBytes { get; set; } = DefaultMaximumFileSizeBytes;
    public string StoragePath { get; set; } = "App_Data/TaskAttachments";
    public string[] AllowedExtensions { get; set; } =
        [".pdf", ".docx", ".xlsx", ".png", ".jpg", ".jpeg", ".zip"];
}

public enum TaskCollaborationFailure
{
    None = 0,
    NotFound,
    Forbidden,
    Validation,
    ReadOnly,
    Storage
}

public sealed class TaskCollaborationResult
{
    public bool Succeeded { get; init; }
    public int? ResourceId { get; init; }
    public TaskCollaborationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;

    public static TaskCollaborationResult Success(int? resourceId, string message) =>
        new() { Succeeded = true, ResourceId = resourceId, Message = message };

    public static TaskCollaborationResult Failed(
        TaskCollaborationFailure failure,
        string message) =>
        new() { Failure = failure, Message = message };
}

public sealed class TaskAttachmentDownloadResult
{
    public bool Succeeded { get; init; }
    public TaskCollaborationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;
    public Stream? Content { get; init; }
    public string? FileName { get; init; }
    public string? ContentType { get; init; }

    public static TaskAttachmentDownloadResult Success(
        Stream content,
        string fileName,
        string contentType) => new()
        {
            Succeeded = true,
            Content = content,
            FileName = fileName,
            ContentType = contentType
        };

    public static TaskAttachmentDownloadResult Failed(
        TaskCollaborationFailure failure,
        string message) => new()
        {
            Failure = failure,
            Message = message
        };
}

public sealed class TaskCommentSummary
{
    public int Id { get; init; }
    public string AuthorUserId { get; init; } = string.Empty;
    public string AuthorName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class TaskAttachmentSummary
{
    public int Id { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string Extension { get; init; } = string.Empty;
    public string UploadedByUserId { get; init; } = string.Empty;
    public string UploadedByName { get; init; } = string.Empty;
    public DateTime UploadedAt { get; init; }
}

public sealed class AddTaskCommentRequest
{
    public int ProjectId { get; init; }
    public int SprintId { get; init; }
    public int TaskId { get; init; }
    public string AuthorUserId { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
}

public sealed class DeleteTaskCommentRequest
{
    public int ProjectId { get; init; }
    public int SprintId { get; init; }
    public int TaskId { get; init; }
    public int CommentId { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class UploadTaskAttachmentRequest
{
    public int ProjectId { get; init; }
    public int SprintId { get; init; }
    public int TaskId { get; init; }
    public string UploadedByUserId { get; init; } = string.Empty;
    public string OriginalFileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public Stream Content { get; init; } = Stream.Null;
}

public sealed class DownloadTaskAttachmentRequest
{
    public int ProjectId { get; init; }
    public int SprintId { get; init; }
    public int TaskId { get; init; }
    public int AttachmentId { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class DeleteTaskAttachmentRequest
{
    public int ProjectId { get; init; }
    public int SprintId { get; init; }
    public int TaskId { get; init; }
    public int AttachmentId { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}
