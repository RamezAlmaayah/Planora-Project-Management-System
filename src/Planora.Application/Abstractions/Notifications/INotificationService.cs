using Planora.Application.Common.Notifications;

namespace Planora.Application.Abstractions.Notifications;

public interface INotificationService
{
    Task<bool> EnqueueAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default);

    Task<int> EnqueueProjectRoleAsync(
        CreateProjectRoleNotificationsRequest request,
        CancellationToken cancellationToken = default);

    Task<int> EnqueueProjectMembersAsync(
        CreateProjectMemberNotificationsRequest request,
        CancellationToken cancellationToken = default);

    Task<NotificationPage> GetPageAsync(
        string userId,
        int page,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationSummary>> GetRecentAsync(
        string userId,
        int take = 7,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<NotificationOperationResult> MarkReadAsync(
        string userId,
        int notificationId,
        CancellationToken cancellationToken = default);

    Task<int> MarkAllReadAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
