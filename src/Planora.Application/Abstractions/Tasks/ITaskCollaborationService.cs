using Planora.Application.Common.Tasks;

namespace Planora.Application.Abstractions.Tasks;

public interface ITaskCollaborationService
{
    Task<TaskCollaborationResult> AddCommentAsync(
        AddTaskCommentRequest request,
        CancellationToken cancellationToken = default);

    Task<TaskCollaborationResult> DeleteCommentAsync(
        DeleteTaskCommentRequest request,
        CancellationToken cancellationToken = default);

    Task<TaskCollaborationResult> UploadAttachmentAsync(
        UploadTaskAttachmentRequest request,
        CancellationToken cancellationToken = default);

    Task<TaskAttachmentDownloadResult> DownloadAttachmentAsync(
        DownloadTaskAttachmentRequest request,
        CancellationToken cancellationToken = default);

    Task<TaskCollaborationResult> DeleteAttachmentAsync(
        DeleteTaskAttachmentRequest request,
        CancellationToken cancellationToken = default);
}
