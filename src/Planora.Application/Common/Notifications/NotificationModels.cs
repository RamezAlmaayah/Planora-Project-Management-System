using Planora.Domain.Enums;

namespace Planora.Application.Common.Notifications;

public sealed class NotificationSummary
{
    public int Id { get; init; }
    public int? ProjectId { get; init; }
    public NotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? TargetUrl { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReadAt { get; init; }
}

public sealed class NotificationPage
{
    public IReadOnlyList<NotificationSummary> Items { get; init; } = [];
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages { get; init; }
}

public sealed class CreateNotificationRequest
{
    public string RecipientUserId { get; init; } = string.Empty;
    public int? ProjectId { get; init; }
    public NotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? TargetUrl { get; init; }
}

public sealed class CreateProjectRoleNotificationsRequest
{
    public int ProjectId { get; init; }
    public ProjectMemberRole ProjectRole { get; init; }
    public string? ExcludedUserId { get; init; }
    public NotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? TargetUrl { get; init; }
}

public sealed class CreateProjectMemberNotificationsRequest
{
    public int ProjectId { get; init; }
    public string? ExcludedUserId { get; init; }
    public NotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? TargetUrl { get; init; }
}

public sealed class NotificationOperationResult
{
    public bool Succeeded { get; init; }
    public string Message { get; init; } = string.Empty;

    public static NotificationOperationResult Success() => new() { Succeeded = true };
    public static NotificationOperationResult NotFound() => new()
    {
        Message = "Notification was not found."
    };
}
