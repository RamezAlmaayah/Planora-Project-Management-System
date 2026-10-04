using Microsoft.EntityFrameworkCore;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Notifications;
using Planora.Application.Common.Notifications;
using Planora.Application.Common.Security;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Notifications;

public sealed class NotificationService : INotificationService
{
    private const int MaximumTitleLength = 200;
    private const int MaximumMessageLength = 1000;
    private const int MaximumTargetUrlLength = 500;
    private static readonly string[] AllowedTargetPrefixes =
    [
        "/Tasks/", "/Issues/", "/Sprints/", "/Projects/"
    ];

    private readonly ApplicationDbContext _context;
    private readonly IClock _clock;

    public NotificationService(ApplicationDbContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<bool> EnqueueAsync(
        CreateNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!IsValidRequest(request))
            return false;

        bool recipientExists = await _context.Users.AsNoTracking()
            .AnyAsync(user => user.Id == request.RecipientUserId, cancellationToken);
        if (!recipientExists)
            return false;

        if (request.ProjectId.HasValue)
        {
            bool validProjectRecipient = await _context.ProjectMembers.AsNoTracking()
                .AnyAsync(member =>
                    member.ProjectId == request.ProjectId.Value &&
                    member.UserId == request.RecipientUserId,
                    cancellationToken);
            if (!validProjectRecipient)
                return false;
        }

        if (IsPendingDuplicate(request))
            return false;

        _context.Notifications.Add(ToEntity(request));
        return true;
    }

    public async Task<int> EnqueueProjectRoleAsync(
        CreateProjectRoleNotificationsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProjectId <= 0 || !Enum.IsDefined(request.ProjectRole) ||
            !IsValidPayload(request.Type, request.Title, request.Message, request.TargetUrl))
            return 0;

        string requiredGlobalRole = request.ProjectRole switch
        {
            ProjectMemberRole.ProjectManager => SystemRoles.ProjectManager,
            ProjectMemberRole.ScrumMaster => SystemRoles.ScrumMaster,
            ProjectMemberRole.Developer => SystemRoles.Developer,
            ProjectMemberRole.QaTester => SystemRoles.QaTester,
            _ => string.Empty
        };
        if (requiredGlobalRole.Length == 0)
            return 0;

        List<string> recipients = await (
            from member in _context.ProjectMembers.AsNoTracking()
            join user in _context.Users.AsNoTracking()
                on member.UserId equals user.Id
            join userRole in _context.UserRoles.AsNoTracking()
                on user.Id equals userRole.UserId
            join role in _context.Roles.AsNoTracking()
                on userRole.RoleId equals role.Id
            where member.ProjectId == request.ProjectId &&
                  member.Role == request.ProjectRole &&
                  role.Name == requiredGlobalRole &&
                  (request.ExcludedUserId == null || user.Id != request.ExcludedUserId)
            select user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        int count = 0;
        foreach (string recipient in recipients)
        {
            if (await EnqueueAsync(new CreateNotificationRequest
                {
                    RecipientUserId = recipient,
                    ProjectId = request.ProjectId,
                    Type = request.Type,
                    Title = request.Title,
                    Message = request.Message,
                    TargetUrl = request.TargetUrl
                }, cancellationToken))
            {
                count++;
            }
        }
        return count;
    }

    public async Task<int> EnqueueProjectMembersAsync(
        CreateProjectMemberNotificationsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ProjectId <= 0 ||
            !IsValidPayload(request.Type, request.Title, request.Message, request.TargetUrl))
            return 0;

        List<string> recipients = await _context.ProjectMembers.AsNoTracking()
            .Where(member => member.ProjectId == request.ProjectId &&
                (request.ExcludedUserId == null || member.UserId != request.ExcludedUserId))
            .Select(member => member.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        int count = 0;
        foreach (string recipient in recipients)
        {
            if (await EnqueueAsync(new CreateNotificationRequest
                {
                    RecipientUserId = recipient,
                    ProjectId = request.ProjectId,
                    Type = request.Type,
                    Title = request.Title,
                    Message = request.Message,
                    TargetUrl = request.TargetUrl
                }, cancellationToken))
            {
                count++;
            }
        }
        return count;
    }

    public async Task<NotificationPage> GetPageAsync(
        string userId,
        int page,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        string normalizedUserId = userId?.Trim() ?? string.Empty;
        int normalizedPageSize = Math.Clamp(pageSize, 1, 50);
        IQueryable<Notification> query = _context.Notifications.AsNoTracking()
            .Where(notification => notification.UserId == normalizedUserId);
        int totalCount = normalizedUserId.Length == 0
            ? 0
            : await query.CountAsync(cancellationToken);
        int totalPages = Math.Max(1,
            (int)Math.Ceiling((double)totalCount / normalizedPageSize));
        int currentPage = Math.Clamp(page, 1, totalPages);

        List<NotificationSummary> items = normalizedUserId.Length == 0
            ? []
            : await Project(query)
                .OrderByDescending(notification => notification.CreatedAt)
                .ThenByDescending(notification => notification.Id)
                .Skip((currentPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync(cancellationToken);

        return new NotificationPage
        {
            Items = items,
            Page = currentPage,
            PageSize = normalizedPageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        };
    }

    public async Task<IReadOnlyList<NotificationSummary>> GetRecentAsync(
        string userId,
        int take = 7,
        CancellationToken cancellationToken = default)
    {
        string normalizedUserId = userId?.Trim() ?? string.Empty;
        if (normalizedUserId.Length == 0)
            return [];

        return await Project(_context.Notifications.AsNoTracking()
                .Where(notification => notification.UserId == normalizedUserId))
            .OrderByDescending(notification => notification.CreatedAt)
            .ThenByDescending(notification => notification.Id)
            .Take(Math.Clamp(take, 1, 10))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        string normalizedUserId = userId?.Trim() ?? string.Empty;
        return normalizedUserId.Length == 0
            ? 0
            : await _context.Notifications.AsNoTracking().CountAsync(
                notification => notification.UserId == normalizedUserId &&
                                !notification.IsRead,
                cancellationToken);
    }

    public async Task<NotificationOperationResult> MarkReadAsync(
        string userId,
        int notificationId,
        CancellationToken cancellationToken = default)
    {
        string normalizedUserId = userId?.Trim() ?? string.Empty;
        if (normalizedUserId.Length == 0 || notificationId <= 0)
            return NotificationOperationResult.NotFound();

        Notification? notification = await _context.Notifications
            .SingleOrDefaultAsync(item =>
                item.Id == notificationId && item.UserId == normalizedUserId,
                cancellationToken);
        if (notification is null)
            return NotificationOperationResult.NotFound();
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = _clock.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
        return NotificationOperationResult.Success();
    }

    public async Task<int> MarkAllReadAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        string normalizedUserId = userId?.Trim() ?? string.Empty;
        if (normalizedUserId.Length == 0)
            return 0;

        DateTime readAt = _clock.UtcNow;
        IQueryable<Notification> query = _context.Notifications
            .Where(notification => notification.UserId == normalizedUserId &&
                                   !notification.IsRead);
        if (_context.Database.IsRelational())
        {
            return await query.ExecuteUpdateAsync(setters => setters
                    .SetProperty(notification => notification.IsRead, true)
                    .SetProperty(notification => notification.ReadAt, readAt),
                cancellationToken);
        }

        List<Notification> notifications = await query.ToListAsync(cancellationToken);
        foreach (Notification notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = readAt;
        }
        if (notifications.Count > 0)
            await _context.SaveChangesAsync(cancellationToken);
        return notifications.Count;
    }

    private static IQueryable<NotificationSummary> Project(IQueryable<Notification> query) =>
        query.Select(notification => new NotificationSummary
        {
            Id = notification.Id,
            ProjectId = notification.ProjectId,
            Type = notification.Type,
            Title = notification.Title,
            Message = notification.Message,
            TargetUrl = notification.TargetUrl != null &&
                (notification.TargetUrl.StartsWith("/Tasks/") ||
                 notification.TargetUrl.StartsWith("/Issues/") ||
                 notification.TargetUrl.StartsWith("/Sprints/") ||
                 notification.TargetUrl.StartsWith("/Projects/"))
                    ? notification.TargetUrl
                    : null,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt,
            ReadAt = notification.ReadAt
        });

    private bool IsPendingDuplicate(CreateNotificationRequest request) =>
        _context.ChangeTracker.Entries<Notification>().Any(entry =>
            entry.State == EntityState.Added &&
            entry.Entity.UserId == request.RecipientUserId &&
            entry.Entity.ProjectId == request.ProjectId &&
            entry.Entity.Type == request.Type &&
            entry.Entity.Title == request.Title &&
            entry.Entity.Message == request.Message &&
            entry.Entity.TargetUrl == request.TargetUrl);

    private Notification ToEntity(CreateNotificationRequest request) => new()
    {
        UserId = request.RecipientUserId,
        ProjectId = request.ProjectId,
        Type = request.Type,
        Title = request.Title.Trim(),
        Message = request.Message.Trim(),
        TargetUrl = request.TargetUrl,
        CreatedAt = _clock.UtcNow
    };

    private static bool IsValidRequest(CreateNotificationRequest request) =>
        !string.IsNullOrWhiteSpace(request.RecipientUserId) &&
        request.RecipientUserId.Trim().Length <= 450 &&
        (!request.ProjectId.HasValue || request.ProjectId.Value > 0) &&
        IsValidPayload(request.Type, request.Title, request.Message, request.TargetUrl);

    private static bool IsValidPayload(
        NotificationType type,
        string title,
        string message,
        string? targetUrl) =>
        Enum.IsDefined(type) &&
        !string.IsNullOrWhiteSpace(title) && title.Trim().Length <= MaximumTitleLength &&
        !string.IsNullOrWhiteSpace(message) && message.Trim().Length <= MaximumMessageLength &&
        IsSafeTargetUrl(targetUrl);

    private static bool IsSafeTargetUrl(string? targetUrl)
    {
        if (targetUrl is null)
            return true;
        if (targetUrl.Length == 0 || targetUrl.Length > MaximumTargetUrlLength ||
            targetUrl.Contains('\\') || targetUrl.Contains("//", StringComparison.Ordinal))
            return false;
        return AllowedTargetPrefixes.Any(prefix =>
            targetUrl.StartsWith(prefix, StringComparison.Ordinal));
    }
}
