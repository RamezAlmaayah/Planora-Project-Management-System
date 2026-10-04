using System.IO.Compression;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Tasks;
using Planora.Application.Common.Security;
using Planora.Application.Common.Tasks;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Tasks;

public sealed class TaskCollaborationService : ITaskCollaborationService
{
    private const int CommentMaximumLength = 4000;
    private const int FileNameMaximumLength = 255;
    private const int MaximumArchiveEntries = 2048;
    private const long MaximumExpandedArchiveBytes = 100L * 1024 * 1024;
    private static readonly HashSet<string> DangerousArchiveExtensions = new(
        [
            ".exe", ".dll", ".bat", ".cmd", ".ps1", ".sh", ".js", ".cshtml",
            ".html", ".htm", ".php", ".asp", ".aspx", ".config", ".com", ".msi",
            ".scr", ".vbs", ".jar"
        ],
        StringComparer.OrdinalIgnoreCase);
    private readonly ApplicationDbContext _context;
    private readonly ITaskAttachmentStorage _storage;
    private readonly IClock _clock;
    private readonly TaskAttachmentOptions _options;
    private readonly ILogger<TaskCollaborationService> _logger;

    public TaskCollaborationService(
        ApplicationDbContext context,
        ITaskAttachmentStorage storage,
        IClock clock,
        IOptions<TaskAttachmentOptions> options,
        ILogger<TaskCollaborationService> logger)
    {
        _context = context;
        _storage = storage;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TaskCollaborationResult> AddCommentAsync(
        AddTaskCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        string content = request.Content?.Trim() ?? string.Empty;
        if (content.Length == 0)
            return Invalid("Comment text is required.");
        if (content.Length > CommentMaximumLength)
            return Invalid($"Comment text must be {CommentMaximumLength} characters or fewer.");

        var validation = await ValidateTaskAccessAsync(
            request.ProjectId, request.SprintId, request.TaskId,
            request.AuthorUserId, cancellationToken);
        if (validation.Error is not null)
            return validation.Error;
        if (validation.Scope!.IsReadOnly)
            return ReadOnly();

        var comment = new TaskComment
        {
            TaskItemId = request.TaskId,
            AuthorUserId = request.AuthorUserId,
            Content = content,
            CreatedAt = _clock.UtcNow
        };
        _context.TaskComments.Add(comment);
        await _context.SaveChangesAsync(cancellationToken);
        return TaskCollaborationResult.Success(comment.Id, "Comment added.");
    }

    public async Task<TaskCollaborationResult> DeleteCommentAsync(
        DeleteTaskCommentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateTaskAccessAsync(
            request.ProjectId, request.SprintId, request.TaskId,
            request.ActorUserId, cancellationToken);
        if (validation.Error is not null)
            return validation.Error;
        if (validation.Scope!.IsReadOnly)
            return ReadOnly();

        TaskComment? comment = await _context.TaskComments
            .SingleOrDefaultAsync(item =>
                item.Id == request.CommentId &&
                item.TaskItemId == request.TaskId,
                cancellationToken);
        if (comment is null)
            return NotFound("The comment was not found on this task.");
        if (!validation.Access!.IsAdmin && comment.AuthorUserId != request.ActorUserId)
            return Forbidden("You can delete only your own comments.");

        _context.TaskComments.Remove(comment);
        await _context.SaveChangesAsync(cancellationToken);
        return TaskCollaborationResult.Success(comment.Id, "Comment deleted.");
    }

    public async Task<TaskCollaborationResult> UploadAttachmentAsync(
        UploadTaskAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateTaskAccessAsync(
            request.ProjectId, request.SprintId, request.TaskId,
            request.UploadedByUserId, cancellationToken);
        if (validation.Error is not null)
            return validation.Error;
        if (validation.Scope!.IsReadOnly)
            return ReadOnly();
        if (!CanUpload(validation.Access!))
            return Forbidden("You are not authorized to upload task attachments.");

        var file = await ValidateAndReadFileAsync(request, cancellationToken);
        if (file.Error is not null)
            return file.Error;

        string storageKey;
        try
        {
            file.Content!.Position = 0;
            storageKey = await _storage.SaveAsync(
                file.Content, file.Extension!, cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Task attachment storage failed for task {TaskId} in project {ProjectId}.",
                request.TaskId, request.ProjectId);
            return StorageFailure("The attachment could not be stored.");
        }

        var attachment = new TaskAttachment
        {
            TaskItemId = request.TaskId,
            OriginalFileName = file.SafeFileName!,
            StorageKey = storageKey,
            FileSizeBytes = file.Content!.Length,
            Extension = file.Extension!,
            UploadedByUserId = request.UploadedByUserId,
            UploadedAt = _clock.UtcNow
        };
        _context.TaskAttachments.Add(attachment);
        _context.ActivityLogs.Add(CreateAttachmentAudit(
            "TaskAttachmentUploaded", attachment, request.ProjectId,
            request.UploadedByUserId, "Task attachment uploaded."));

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            try
            {
                await _storage.DeleteAsync(storageKey, CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                _logger.LogError(cleanupException,
                    "Cleanup failed after task attachment persistence failure for task {TaskId}.",
                    request.TaskId);
            }

            _logger.LogError(exception,
                "Task attachment metadata persistence failed for task {TaskId} in project {ProjectId}.",
                request.TaskId, request.ProjectId);
            return StorageFailure("The attachment could not be saved.");
        }

        return TaskCollaborationResult.Success(attachment.Id, "Attachment uploaded.");
    }

    public async Task<TaskAttachmentDownloadResult> DownloadAttachmentAsync(
        DownloadTaskAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateTaskAccessAsync(
            request.ProjectId, request.SprintId, request.TaskId,
            request.ActorUserId, cancellationToken);
        if (validation.Error is not null)
            return DownloadFailure(validation.Error);

        TaskAttachment? attachment = await _context.TaskAttachments.AsNoTracking()
            .SingleOrDefaultAsync(item =>
                item.Id == request.AttachmentId &&
                item.TaskItemId == request.TaskId,
                cancellationToken);
        if (attachment is null)
            return TaskAttachmentDownloadResult.Failed(
                TaskCollaborationFailure.NotFound,
                "The attachment was not found on this task.");

        try
        {
            Stream? stream = await _storage.OpenReadAsync(
                attachment.StorageKey, cancellationToken);
            return stream is null
                ? TaskAttachmentDownloadResult.Failed(
                    TaskCollaborationFailure.NotFound,
                    "The attachment file is unavailable.")
                : TaskAttachmentDownloadResult.Success(
                    stream,
                    attachment.OriginalFileName,
                    GetContentType(attachment.Extension));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Task attachment download failed for attachment {AttachmentId}.",
                request.AttachmentId);
            return TaskAttachmentDownloadResult.Failed(
                TaskCollaborationFailure.Storage,
                "The attachment file is unavailable.");
        }
    }

    public async Task<TaskCollaborationResult> DeleteAttachmentAsync(
        DeleteTaskAttachmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateTaskAccessAsync(
            request.ProjectId, request.SprintId, request.TaskId,
            request.ActorUserId, cancellationToken);
        if (validation.Error is not null)
            return validation.Error;
        if (validation.Scope!.IsReadOnly)
            return ReadOnly();

        TaskAttachment? attachment = await _context.TaskAttachments
            .SingleOrDefaultAsync(item =>
                item.Id == request.AttachmentId &&
                item.TaskItemId == request.TaskId,
                cancellationToken);
        if (attachment is null)
            return NotFound("The attachment was not found on this task.");
        if (!CanDeleteAttachment(validation.Access!, attachment, request.ActorUserId))
            return Forbidden("You are not authorized to delete this attachment.");

        byte[]? backup = null;
        try
        {
            await using Stream? source = await _storage.OpenReadAsync(
                attachment.StorageKey, cancellationToken);
            if (source is not null)
            {
                using var buffer = new MemoryStream();
                await source.CopyToAsync(buffer, cancellationToken);
                backup = buffer.ToArray();
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Task attachment could not be prepared for deletion for attachment {AttachmentId}.",
                request.AttachmentId);
            return StorageFailure("The attachment could not be deleted.");
        }

        _context.TaskAttachments.Remove(attachment);
        _context.ActivityLogs.Add(CreateAttachmentAudit(
            "TaskAttachmentDeleted", attachment, request.ProjectId,
            request.ActorUserId, "Task attachment deleted."));

        await using IDbContextTransaction? transaction =
            await BeginTransactionAsync(cancellationToken);
        bool physicalFileDeleted = false;
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            await _storage.DeleteAsync(attachment.StorageKey, cancellationToken);
            physicalFileDeleted = true;
            if (transaction is not null)
                await transaction.CommitAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            if (transaction is not null)
                await transaction.RollbackAsync(CancellationToken.None);
            if (physicalFileDeleted && backup is not null)
            {
                try
                {
                    await _storage.RestoreAsync(
                        attachment.StorageKey,
                        new MemoryStream(backup, writable: false),
                        CancellationToken.None);
                }
                catch (Exception restoreException)
                {
                    _logger.LogError(restoreException,
                        "Task attachment restoration failed for attachment {AttachmentId}.",
                        request.AttachmentId);
                }
            }

            _logger.LogError(exception,
                "Task attachment deletion failed for attachment {AttachmentId}.",
                request.AttachmentId);
            return StorageFailure("The attachment could not be deleted.");
        }

        return TaskCollaborationResult.Success(attachment.Id, "Attachment deleted.");
    }

    private async Task<(TaskResourceScope? Scope, ActorAccess? Access, TaskCollaborationResult? Error)>
        ValidateTaskAccessAsync(
            int projectId,
            int sprintId,
            int taskId,
            string actorUserId,
            CancellationToken cancellationToken)
    {
        if (projectId <= 0 || sprintId <= 0 || taskId <= 0 ||
            string.IsNullOrWhiteSpace(actorUserId))
            return (null, null, Invalid("The task request is invalid."));

        TaskResourceScope? scope = await _context.TaskItems.AsNoTracking()
            .Where(task =>
                task.Id == taskId &&
                task.SprintBacklogItem.SprintId == sprintId &&
                task.SprintBacklogItem.Sprint.ProjectId == projectId &&
                task.SprintBacklogItem.BacklogItem.ProjectId == projectId &&
                task.SprintBacklogItem.Sprint.Project.Methodology == ProjectMethodology.Scrum)
            .Select(task => new TaskResourceScope(
                task.SprintBacklogItem.Sprint.Project.Status,
                task.SprintBacklogItem.Sprint.Status,
                task.SprintBacklogItem.BacklogItem.Status))
            .SingleOrDefaultAsync(cancellationToken);
        if (scope is null)
            return (null, null, NotFound("The project, sprint or task was not found."));

        List<string?> roles = await (
            from userRole in _context.UserRoles.AsNoTracking()
            join role in _context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where userRole.UserId == actorUserId
            select role.Name)
            .ToListAsync(cancellationToken);
        if (!roles.Any(role => role is not null && SystemRoles.All.Contains(role)))
            return (null, null, Forbidden("You are not authorized to access tasks."));

        ProjectMemberRole? projectRole = await _context.ProjectMembers.AsNoTracking()
            .Where(member => member.ProjectId == projectId && member.UserId == actorUserId)
            .Select(member => (ProjectMemberRole?)member.Role)
            .SingleOrDefaultAsync(cancellationToken);
        bool isAdmin = roles.Contains(SystemRoles.Admin, StringComparer.Ordinal);
        if (!isAdmin && projectRole is null)
            return (null, null, Forbidden("You are not a member of this project."));

        return (scope, new ActorAccess(
            isAdmin,
            roles.Contains(SystemRoles.ProjectManager, StringComparer.Ordinal),
            roles.Contains(SystemRoles.ScrumMaster, StringComparer.Ordinal),
            projectRole), null);
    }

    private async Task<(MemoryStream? Content, string? SafeFileName, string? Extension,
        TaskCollaborationResult? Error)> ValidateAndReadFileAsync(
            UploadTaskAttachmentRequest request,
            CancellationToken cancellationToken)
    {
        if (request.Content == Stream.Null || request.FileSizeBytes <= 0)
            return (null, null, null, Invalid("Select a non-empty file."));
        if (request.FileSizeBytes > _options.MaximumFileSizeBytes)
            return (null, null, null, Invalid("Attachments must be 10 MB or smaller."));

        string normalizedName = (request.OriginalFileName ?? string.Empty)
            .Replace('\\', '/');
        string safeFileName = Path.GetFileName(normalizedName).Trim();
        if (safeFileName.Length == 0 || safeFileName.Length > FileNameMaximumLength)
            return (null, null, null, Invalid("The attachment file name is invalid."));

        string extension = Path.GetExtension(safeFileName).ToLowerInvariant();
        var allowedExtensions = _options.AllowedExtensions
            .Select(item => item.StartsWith('.') ? item.ToLowerInvariant() : $".{item.ToLowerInvariant()}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!allowedExtensions.Contains(extension))
            return (null, null, null, Invalid("This attachment file type is not allowed."));
        if (!IsAllowedContentType(extension, request.ContentType))
            return (null, null, null, Invalid("The attachment content type does not match its extension."));

        var content = new MemoryStream();
        byte[] buffer = new byte[81920];
        while (true)
        {
            int read = await request.Content.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;
            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            if (content.Length > _options.MaximumFileSizeBytes)
            {
                content.Dispose();
                return (null, null, null, Invalid("Attachments must be 10 MB or smaller."));
            }
        }

        if (content.Length == 0 || content.Length != request.FileSizeBytes)
        {
            content.Dispose();
            return (null, null, null, Invalid("The attachment file is empty or incomplete."));
        }
        if (!HasValidFileSignature(extension, content.ToArray()))
        {
            content.Dispose();
            return (null, null, null, Invalid("The attachment content does not match its extension."));
        }

        content.Position = 0;
        return (content, safeFileName, extension, null);
    }

    private static bool HasValidFileSignature(string extension, byte[] content)
    {
        return extension switch
        {
            ".pdf" => content.AsSpan().StartsWith("%PDF-"u8),
            ".png" => content.AsSpan().StartsWith(
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".jpg" or ".jpeg" => content.Length >= 3 &&
                content[0] == 0xFF && content[1] == 0xD8 && content[2] == 0xFF,
            ".zip" => IsValidZip(content, null),
            ".docx" => IsValidZip(content, "word/"),
            ".xlsx" => IsValidZip(content, "xl/"),
            _ => false
        };
    }

    private static bool IsValidZip(byte[] content, string? requiredFolder)
    {
        try
        {
            using var archive = new ZipArchive(
                new MemoryStream(content, writable: false),
                ZipArchiveMode.Read);
            if (archive.Entries.Count == 0 || archive.Entries.Count > MaximumArchiveEntries)
                return false;
            long expandedBytes = 0;
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (!IsSafeArchiveEntry(entry.FullName))
                    return false;
                expandedBytes = checked(expandedBytes + entry.Length);
                if (expandedBytes > MaximumExpandedArchiveBytes)
                    return false;
            }
            if (requiredFolder is null)
                return true;
            return archive.Entries.Any(entry =>
                entry.FullName.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase)) &&
                archive.Entries.Any(entry =>
                    entry.FullName.StartsWith(requiredFolder, StringComparison.OrdinalIgnoreCase));
        }
        catch (InvalidDataException)
        {
            return false;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool IsSafeArchiveEntry(string entryName)
    {
        if (string.IsNullOrWhiteSpace(entryName) ||
            entryName.Any(char.IsControl) ||
            entryName.StartsWith('/') ||
            entryName.StartsWith('\\') ||
            Path.IsPathRooted(entryName.Replace('/', Path.DirectorySeparatorChar)))
            return false;

        string normalized = entryName.Replace('\\', '/');
        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment is "." or ".."))
            return false;

        string extension = Path.GetExtension(normalized);
        return !DangerousArchiveExtensions.Contains(extension);
    }

    private static bool IsAllowedContentType(string extension, string contentType)
    {
        string normalized = contentType?.Trim().ToLowerInvariant() ?? string.Empty;
        return extension switch
        {
            ".pdf" => normalized == "application/pdf",
            ".png" => normalized == "image/png",
            ".jpg" or ".jpeg" => normalized == "image/jpeg",
            ".docx" => normalized ==
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => normalized ==
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".zip" => normalized is "application/zip" or "application/x-zip-compressed",
            _ => false
        };
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };

    private static bool CanUpload(ActorAccess access) =>
        access.IsAdmin ||
        access.HasProjectManagerRole && access.ProjectRole == ProjectMemberRole.ProjectManager ||
        access.HasScrumMasterRole && access.ProjectRole == ProjectMemberRole.ScrumMaster;

    private static bool CanDeleteAttachment(
        ActorAccess access,
        TaskAttachment attachment,
        string actorUserId) =>
        access.IsAdmin ||
        access.HasProjectManagerRole && access.ProjectRole == ProjectMemberRole.ProjectManager ||
        access.HasScrumMasterRole && access.ProjectRole == ProjectMemberRole.ScrumMaster &&
            attachment.UploadedByUserId == actorUserId;

    private ActivityLog CreateAttachmentAudit(
        string action,
        TaskAttachment attachment,
        int projectId,
        string actorUserId,
        string description) => new()
        {
            ActorUserId = actorUserId,
            ProjectId = projectId,
            Action = action,
            ResourceType = nameof(TaskAttachment),
            ResourceId = attachment.TaskItemId.ToString(),
            Description = description,
            NewValues = JsonSerializer.Serialize(new
            {
                attachment.TaskItemId,
                attachment.OriginalFileName,
                attachment.FileSizeBytes
            }),
            CreatedAt = _clock.UtcNow
        };

    private async Task<IDbContextTransaction?> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        if (!_context.Database.IsRelational())
            return null;
        return await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    private static TaskCollaborationResult Invalid(string message) =>
        TaskCollaborationResult.Failed(TaskCollaborationFailure.Validation, message);
    private static TaskCollaborationResult NotFound(string message) =>
        TaskCollaborationResult.Failed(TaskCollaborationFailure.NotFound, message);
    private static TaskCollaborationResult Forbidden(string message) =>
        TaskCollaborationResult.Failed(TaskCollaborationFailure.Forbidden, message);
    private static TaskCollaborationResult ReadOnly() =>
        TaskCollaborationResult.Failed(
            TaskCollaborationFailure.ReadOnly,
            "This task is read-only because its project, sprint or backlog item no longer allows changes.");
    private static TaskCollaborationResult StorageFailure(string message) =>
        TaskCollaborationResult.Failed(TaskCollaborationFailure.Storage, message);
    private static TaskAttachmentDownloadResult DownloadFailure(TaskCollaborationResult result) =>
        TaskAttachmentDownloadResult.Failed(result.Failure, result.Message);

    private sealed record TaskResourceScope(
        ProjectStatus ProjectStatus,
        SprintStatus SprintStatus,
        BacklogItemStatus BacklogStatus)
    {
        public bool IsReadOnly =>
            ProjectStatus == ProjectStatus.Archived ||
            SprintStatus == SprintStatus.Completed ||
            BacklogStatus != BacklogItemStatus.InSprint;
    }

    private sealed record ActorAccess(
        bool IsAdmin,
        bool HasProjectManagerRole,
        bool HasScrumMasterRole,
        ProjectMemberRole? ProjectRole);
}
