using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Security;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;
using Planora.Web.Presence;
using Planora.Web.ViewModels.AdminActivity;

namespace Planora.Web.Controllers;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
[Route("Admin/Activity")]
public sealed class AdminActivityController : Controller
{
    private const int PageSize = 30;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserPresenceRegistry _presence;
    private readonly IClock _clock;

    public AdminActivityController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        UserPresenceRegistry presence, IClock clock)
    { _db = db; _userManager = userManager; _presence = presence; _clock = clock; }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? userId, int? projectId, string? action,
        DateTime? fromDate, DateTime? toDate, int page = 1, CancellationToken cancellationToken = default)
    {
        DateTime from = (fromDate ?? _clock.UtcNow.Date.AddDays(-29)).Date;
        DateTime to = (toDate ?? _clock.UtcNow.Date).Date.AddDays(1);
        var query = _db.ActivityLogs.AsNoTracking().Where(log => log.CreatedAt >= from && log.CreatedAt < to);
        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(log => log.ActorUserId == userId);
        if (projectId.HasValue) query = query.Where(log => log.ProjectId == projectId.Value);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(log => log.Action == action);
        int total = await query.CountAsync(cancellationToken);
        int pages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        int current = Math.Clamp(page, 1, pages);
        var rows = await query.OrderByDescending(log => log.CreatedAt).ThenByDescending(log => log.Id)
            .Skip((current - 1) * PageSize).Take(PageSize)
            .Select(log => new
            {
                log.Id, log.ActorUserId, log.Action, log.ResourceType, log.ResourceId, log.ProjectId, log.CreatedAt,
                Actor = _db.Users.Where(user => user.Id == log.ActorUserId)
                    .Select(user => user.FullName.Length > 0 ? user.FullName : user.UserName ?? "User").FirstOrDefault() ?? "User",
                Project = _db.Projects.Where(project => project.Id == log.ProjectId).Select(project => project.Name).FirstOrDefault()
            }).ToListAsync(cancellationToken);

        var users = await _db.Users.AsNoTracking().OrderBy(user => user.FullName)
            .Take(500).Select(user => new ActivityUserOption(user.Id,
                user.FullName.Length > 0 ? user.FullName : user.UserName ?? "User")).ToListAsync(cancellationToken);
        var projects = await _db.Projects.AsNoTracking().OrderBy(project => project.Name).Take(500)
            .Select(project => new ActivityProjectOption(project.Id, project.Name)).ToListAsync(cancellationToken);
        var actions = await _db.ActivityLogs.AsNoTracking().Select(log => log.Action).Distinct()
            .OrderBy(value => value).Take(200).ToListAsync(cancellationToken);
        var presenceRows = await _db.Users.AsNoTracking().OrderBy(user => user.FullName)
            .Select(user => new { user.Id, user.FullName, user.UserName, user.LastSeenAt }).ToListAsync(cancellationToken);
        var presence = new List<PresenceRow>(presenceRows.Count);
        foreach (var user in presenceRows)
        {
            ApplicationUser? identity = await _userManager.FindByIdAsync(user.Id);
            string role = identity is null ? "" : string.Join(", ", await _userManager.GetRolesAsync(identity));
            presence.Add(new PresenceRow(user.FullName.Length > 0 ? user.FullName : user.UserName ?? "User",
                role, _presence.IsOnline(user.Id), user.LastSeenAt));
        }

        return View(new AdminActivityViewModel
        {
            UserId = userId, ProjectId = projectId, Action = action, FromDate = from, ToDate = to.AddDays(-1),
            Page = current, TotalPages = pages, TotalCount = total,
            Entries = rows.Select(log => new ActivityEntry(log.Id, log.Actor, Humanize(log.Action),
                Humanize(log.ResourceType), log.ResourceId, log.Project, log.CreatedAt)).ToArray(),
            Users = users, Projects = projects, Actions = actions, Presence = presence
        });
    }

    [HttpGet("PresenceSnapshot")]
    public async Task<IActionResult> PresenceSnapshot(CancellationToken cancellationToken)
    {
        var users = await _db.Users.AsNoTracking().OrderBy(user => user.FullName)
            .Select(user => new { user.Id, user.FullName, user.UserName, user.LastSeenAt }).ToListAsync(cancellationToken);
        var result = new List<PresenceRow>(users.Count);
        foreach (var user in users)
        {
            var identity = await _userManager.FindByIdAsync(user.Id);
            result.Add(new PresenceRow(user.FullName.Length > 0 ? user.FullName : user.UserName ?? "User",
                identity is null ? "" : string.Join(", ", await _userManager.GetRolesAsync(identity)),
                _presence.IsOnline(user.Id), user.LastSeenAt));
        }
        return Json(result);
    }

    private static string Humanize(string value) => Regex.Replace(value, "(?<!^)([A-Z])", " $1");
}
