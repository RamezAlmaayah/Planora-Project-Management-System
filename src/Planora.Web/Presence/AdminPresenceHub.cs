using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Security;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;

namespace Planora.Web.Presence;

[Authorize]
public sealed class AdminPresenceHub : Hub
{
    private const string AdminsGroup = "planora-admin-presence";
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly UserPresenceRegistry _presence;
    private readonly IClock _clock;

    public AdminPresenceHub(ApplicationDbContext db, UserManager<ApplicationUser> users,
        UserPresenceRegistry presence, IClock clock)
    {
        _db = db;
        _users = users;
        _presence = presence;
        _clock = clock;
    }

    public async Task<IReadOnlyList<PresenceEntry>> GetSnapshot()
    {
        if (!Context.User!.IsInRole(SystemRoles.Admin)) throw new HubException("Admin access is required.");
        var list = await _db.Users.AsNoTracking().OrderBy(user => user.FullName)
            .Select(user => new { user.Id, user.FullName, user.UserName, user.LastSeenAt })
            .ToListAsync(Context.ConnectionAborted);
        var result = new List<PresenceEntry>(list.Count);
        foreach (var user in list)
        {
            var roles = await _users.GetRolesAsync(await _users.FindByIdAsync(user.Id)
                ?? throw new HubException("Presence user not found."));
            result.Add(new PresenceEntry(user.FullName.Length > 0 ? user.FullName : user.UserName ?? "User",
                string.Join(", ", roles), _presence.IsOnline(user.Id), user.LastSeenAt));
        }
        return result;
    }

    public async Task Touch()
    {
        string userId = Context.UserIdentifier ?? throw new HubException("Authentication required.");
        if (!_presence.IsOnline(userId) || !_presence.TryTouch(userId, TimeSpan.FromMinutes(2))) return;
        var user = await _db.Users.SingleOrDefaultAsync(item => item.Id == userId, Context.ConnectionAborted);
        if (user is null) return;
        user.LastSeenAt = _clock.UtcNow;
        await _db.SaveChangesAsync(Context.ConnectionAborted);
    }

    public override async Task OnConnectedAsync()
    {
        string? userId = Context.UserIdentifier;
        if (string.IsNullOrWhiteSpace(userId)) { Context.Abort(); return; }
        bool cameOnline = _presence.Connect(userId, Context.ConnectionId);
        if (Context.User!.IsInRole(SystemRoles.Admin))
            await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup);
        var user = await _db.Users.SingleOrDefaultAsync(item => item.Id == userId, Context.ConnectionAborted);
        if (user is not null && _presence.TryTouch(userId, TimeSpan.FromMinutes(2)))
        {
            user.LastSeenAt = _clock.UtcNow;
            await _db.SaveChangesAsync(Context.ConnectionAborted);
        }
        if (user is not null && cameOnline)
            await PublishAsync(user, true, Context.ConnectionAborted);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        string? userId = Context.UserIdentifier;
        if (!string.IsNullOrWhiteSpace(userId) && _presence.Disconnect(userId, Context.ConnectionId))
        {
            var user = await _db.Users.SingleOrDefaultAsync(item => item.Id == userId);
            if (user is not null)
            {
                user.LastSeenAt = _clock.UtcNow;
                await _db.SaveChangesAsync();
                await PublishAsync(user, false, CancellationToken.None);
            }
        }
        await base.OnDisconnectedAsync(exception);
    }

    private async Task PublishAsync(ApplicationUser user, bool online, CancellationToken cancellationToken)
    {
        var roles = await _users.GetRolesAsync(user);
        await Clients.Group(AdminsGroup).SendAsync("PresenceChanged",
            new PresenceEntry(user.FullName.Length > 0 ? user.FullName : user.UserName ?? "User",
                string.Join(", ", roles), online, user.LastSeenAt), cancellationToken);
    }

    public sealed record PresenceEntry(string Name, string Role, bool Online, DateTime? LastSeenAt);
}
