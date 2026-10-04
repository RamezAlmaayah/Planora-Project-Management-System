using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Notifications;
using Planora.Application.Common.Notifications;
using Planora.Web.ViewModels.Notifications;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class NotificationsController : Controller
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        string? userId = CurrentUserId();
        if (userId is null) return Challenge();

        return View(new NotificationIndexViewModel
        {
            Page = await _notifications.GetPageAsync(
                userId, page, 20, cancellationToken)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(
        int id,
        CancellationToken cancellationToken = default)
    {
        string? userId = CurrentUserId();
        if (userId is null) return Challenge();

        NotificationOperationResult result = await _notifications.MarkReadAsync(
            userId, id, cancellationToken);
        if (!result.Succeeded) return NotFound(result.Message);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(
        CancellationToken cancellationToken = default)
    {
        string? userId = CurrentUserId();
        if (userId is null) return Challenge();

        await _notifications.MarkAllReadAsync(userId, cancellationToken);
        return RedirectToAction(nameof(Index));
    }

    private string? CurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier);
}
