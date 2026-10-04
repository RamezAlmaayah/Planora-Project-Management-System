using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Notifications;
using Planora.Web.ViewModels.Notifications;

namespace Planora.Web.ViewComponents;

public sealed class NotificationBellViewComponent : ViewComponent
{
    private readonly INotificationService _notifications;

    public NotificationBellViewComponent(INotificationService notifications)
    {
        _notifications = notifications;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        string? userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return View(new NotificationBellViewModel());

        return View(new NotificationBellViewModel
        {
            UnreadCount = await _notifications.GetUnreadCountAsync(userId),
            Recent = await _notifications.GetRecentAsync(userId, 7)
        });
    }
}
