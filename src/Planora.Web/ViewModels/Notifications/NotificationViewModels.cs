using Planora.Application.Common.Notifications;

namespace Planora.Web.ViewModels.Notifications;

public sealed class NotificationIndexViewModel
{
    public NotificationPage Page { get; init; } = new();
}

public sealed class NotificationBellViewModel
{
    public int UnreadCount { get; init; }
    public IReadOnlyList<NotificationSummary> Recent { get; init; } = [];
}
