using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Identity;
using Planora.Application.Common.Identity;
using Planora.Application.Common.Security;
using Planora.Domain.Entities;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Identity;

public sealed class AdminUserService : IAdminUserService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClock _clock;

    public AdminUserService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IClock clock)
    {
        _context = context;
        _userManager = userManager;
        _clock = clock;
    }

    public async Task<AdminUserListResult> GetUsersAsync(
        AdminUserListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        int page = Math.Max(1, query.Page);
        int pageSize = Math.Clamp(query.PageSize, 1, 50);
        string? search = NormalizeOptional(query.Search);
        string? roleFilter = NormalizeOptional(query.Role);

        IQueryable<ApplicationUser> users = _context.Users.AsNoTracking();

        if (search is not null)
        {
            users = users.Where(user =>
                user.FullName.Contains(search) ||
                (user.Email != null && user.Email.Contains(search)));
        }

        if (query.IsDisabled.HasValue)
            users = users.Where(user => user.IsDisabled == query.IsDisabled.Value);

        if (roleFilter is not null)
        {
            users = users.Where(user =>
                (from userRole in _context.UserRoles
                 join role in _context.Roles on userRole.RoleId equals role.Id
                 where userRole.UserId == user.Id && role.Name == roleFilter
                 select userRole.UserId).Any());
        }

        int total = await users.CountAsync(cancellationToken);

        List<AdminUserSummary> items = await users
            .OrderBy(user => user.FullName)
            .ThenBy(user => user.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(user => new AdminUserSummary
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                GlobalRole = (
                    from userRole in _context.UserRoles
                    join role in _context.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == user.Id
                    select role.Name).FirstOrDefault() ?? string.Empty,
                IsDisabled = user.IsDisabled,
                EmailConfirmed = user.EmailConfirmed,
                ProjectMembershipCount = _context.ProjectMembers.Count(member => member.UserId == user.Id),
                CreatedAt = user.CreatedAt,
                LastSeenAt = user.LastSeenAt
            })
            .ToListAsync(cancellationToken);

        return new AdminUserListResult
        {
            Users = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<AdminUserDetails?> GetUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        ApplicationUser? user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == userId, cancellationToken);

        if (user is null)
            return null;

        string role = await (
            from userRole in _context.UserRoles.AsNoTracking()
            join identityRole in _context.Roles.AsNoTracking()
                on userRole.RoleId equals identityRole.Id
            where userRole.UserId == user.Id
            select identityRole.Name!).FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        List<AdminUserProjectMembership> memberships = await _context.ProjectMembers
            .AsNoTracking()
            .Where(member => member.UserId == user.Id)
            .OrderBy(member => member.Project.Name)
            .Select(member => new AdminUserProjectMembership
            {
                MembershipId = member.Id,
                ProjectId = member.ProjectId,
                ProjectName = member.Project.Name,
                Methodology = member.Project.Methodology,
                ProjectStatus = member.Project.Status,
                ProjectRole = member.Role,
                JoinedAt = member.JoinedAt
            })
            .ToListAsync(cancellationToken);

        return new AdminUserDetails
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            GlobalRole = role,
            IsDisabled = user.IsDisabled,
            EmailConfirmed = user.EmailConfirmed,
            ProjectMembershipCount = memberships.Count,
            CreatedAt = user.CreatedAt,
            LastSeenAt = user.LastSeenAt,
            ProjectMemberships = memberships
        };
    }

    public async Task<AdminUserOperationResult> CreateAsync(
        CreateAdminUserRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string fullName = request.FullName.Trim();
        string email = request.Email.Trim();
        string role = request.GlobalRole.Trim();

        if (fullName.Length is < 2 or > 150)
            return Fail("Full name must be between 2 and 150 characters.");
        if (email.Length is < 3 or > 256 || !email.Contains('@'))
            return Fail("Enter a valid email address.");
        if (!SystemRoles.All.Contains(role, StringComparer.Ordinal))
            return Fail("Select a valid global role.");
        if (string.IsNullOrWhiteSpace(request.Password))
            return Fail("An initial password is required.");

        await using IDbContextTransaction? transaction = await BeginTransactionAsync(cancellationToken);

        if (await _userManager.FindByEmailAsync(email) is not null)
            return Fail("A user with this email already exists.");

        var user = new ApplicationUser
        {
            FullName = fullName,
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            CreatedAt = _clock.UtcNow
        };

        IdentityResult created = await _userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return Fail(IdentityErrors(created));

        IdentityResult roleResult = await _userManager.AddToRoleAsync(user, role);
        if (!roleResult.Succeeded)
        {
            await _userManager.DeleteAsync(user);
            return Fail(IdentityErrors(roleResult));
        }

        AddAudit("AdminUserCreated", user.Id, request.ActorUserId,
            $"User account {user.Id} was created with global role {role}.", null, role);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        return AdminUserOperationResult.Success(user.Id);
    }

    public async Task<AdminUserOperationResult> ChangeGlobalRoleAsync(
        ChangeAdminUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        string newRole = request.GlobalRole.Trim();
        if (!SystemRoles.All.Contains(newRole, StringComparer.Ordinal))
            return Fail("Select a valid global role.");
        if (request.UserId == request.ActorUserId)
            return Fail("You cannot change your own global role.");

        await using IDbContextTransaction? transaction = await BeginTransactionAsync(cancellationToken);
        ApplicationUser? user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null) return Fail("User was not found.");

        IList<string> currentRoles = await _userManager.GetRolesAsync(user);
        string oldRole = currentRoles.FirstOrDefault(SystemRoles.All.Contains) ?? string.Empty;
        if (string.Equals(oldRole, newRole, StringComparison.Ordinal))
            return Fail("The user already has this global role.");

        if (oldRole == SystemRoles.Admin && newRole != SystemRoles.Admin &&
            await EnabledAdminCountAsync(cancellationToken) <= 1)
            return Fail("The last enabled Admin cannot be demoted.");

        IdentityResult removeResult = await _userManager.RemoveFromRolesAsync(
            user, currentRoles.Where(SystemRoles.All.Contains));
        if (!removeResult.Succeeded) return Fail(IdentityErrors(removeResult));

        IdentityResult addResult = await _userManager.AddToRoleAsync(user, newRole);
        if (!addResult.Succeeded) return Fail(IdentityErrors(addResult));

        await _userManager.UpdateSecurityStampAsync(user);
        AddAudit("AdminUserGlobalRoleChanged", user.Id, request.ActorUserId,
            $"Global role changed for user {user.Id}.", oldRole, newRole);
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        return AdminUserOperationResult.Success(user.Id);
    }

    public async Task<AdminUserOperationResult> DisableAsync(
        AdminUserStateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId == request.ActorUserId)
            return Fail("You cannot disable your own account.");

        await using IDbContextTransaction? transaction = await BeginTransactionAsync(cancellationToken);
        ApplicationUser? user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null) return Fail("User was not found.");
        if (user.IsDisabled) return Fail("The user is already disabled.");

        if (await _userManager.IsInRoleAsync(user, SystemRoles.Admin) &&
            await EnabledAdminCountAsync(cancellationToken) <= 1)
            return Fail("The last enabled Admin cannot be disabled.");

        user.IsDisabled = true;
        user.DisabledAt = _clock.UtcNow;
        IdentityResult stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded) return Fail(IdentityErrors(stampResult));

        AddAudit("AdminUserDisabled", user.Id, request.ActorUserId,
            $"User account {user.Id} was disabled.");
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        return AdminUserOperationResult.Success(user.Id);
    }

    public async Task<AdminUserOperationResult> EnableAsync(
        AdminUserStateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using IDbContextTransaction? transaction = await BeginTransactionAsync(cancellationToken);
        ApplicationUser? user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null) return Fail("User was not found.");
        if (!user.IsDisabled) return Fail("The user is already enabled.");

        user.IsDisabled = false;
        user.DisabledAt = null;
        IdentityResult stampResult = await _userManager.UpdateSecurityStampAsync(user);
        if (!stampResult.Succeeded) return Fail(IdentityErrors(stampResult));

        AddAudit("AdminUserEnabled", user.Id, request.ActorUserId,
            $"User account {user.Id} was enabled.");
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        return AdminUserOperationResult.Success(user.Id);
    }

    public async Task<AdminUserOperationResult> DeleteAsync(
        AdminUserStateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.UserId == request.ActorUserId)
            return Fail("You cannot delete your own account.");

        await using IDbContextTransaction? transaction = await BeginTransactionAsync(cancellationToken);
        ApplicationUser? user = await _userManager.FindByIdAsync(request.UserId);
        if (user is null) return Fail("User was not found.");

        if (await _userManager.IsInRoleAsync(user, SystemRoles.Admin) &&
            !user.IsDisabled && await EnabledAdminCountAsync(cancellationToken) <= 1)
            return Fail("The last enabled Admin cannot be deleted.");

        if (await HasDomainReferencesAsync(user.Id, cancellationToken))
            return Fail("This user has project or historical records and cannot be permanently deleted safely. Disable the account instead.");

        IdentityResult result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded) return Fail(IdentityErrors(result));

        AddAudit("AdminUserDeleted", user.Id, request.ActorUserId,
            $"Unreferenced user account {user.Id} was permanently deleted.");
        await _context.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);

        return AdminUserOperationResult.Success(user.Id);
    }

    private async Task<int> EnabledAdminCountAsync(CancellationToken cancellationToken)
    {
        return await (
            from user in _context.Users
            join userRole in _context.UserRoles on user.Id equals userRole.UserId
            join role in _context.Roles on userRole.RoleId equals role.Id
            where role.Name == SystemRoles.Admin && !user.IsDisabled
            select user.Id).Distinct().CountAsync(cancellationToken);
    }

    private async Task<bool> HasDomainReferencesAsync(string userId, CancellationToken ct)
    {
        return await _context.ProjectMembers.AnyAsync(x => x.UserId == userId, ct)
            || await _context.Projects.AnyAsync(x => x.CreatedByUserId == userId || x.UpdatedByUserId == userId, ct)
            || await _context.Sprints.AnyAsync(x => x.CreatedByUserId == userId || x.UpdatedByUserId == userId, ct)
            || await _context.BacklogItems.AnyAsync(x => x.CreatedByUserId == userId || x.UpdatedByUserId == userId, ct)
            || await _context.SprintBacklogItems.AnyAsync(x => x.AddedByUserId == userId, ct)
            || await _context.TaskItems.AnyAsync(x => x.AssignedUserId == userId || x.CreatedByUserId == userId || x.UpdatedByUserId == userId, ct)
            || await _context.QaReviews.AnyAsync(x => x.QaUserId == userId, ct)
            || await _context.QaEvidenceFiles.AnyAsync(x => x.UploadedByUserId == userId, ct)
            || await _context.TaskComments.AnyAsync(x => x.AuthorUserId == userId, ct)
            || await _context.TaskAttachments.AnyAsync(x => x.UploadedByUserId == userId, ct)
            || await _context.Issues.AnyAsync(x => x.ReporterUserId == userId || x.AssignedUserId == userId, ct)
            || await _context.Requirements.AnyAsync(x => x.CreatedByUserId == userId || x.UpdatedByUserId == userId, ct)
            || await _context.RequirementDependencies.AnyAsync(x => x.CreatedByUserId == userId, ct)
            || await _context.RequirementTraces.AnyAsync(x => x.CreatedByUserId == userId, ct)
            || await _context.DesignArtifacts.AnyAsync(x => x.CreatedByUserId == userId || x.UpdatedByUserId == userId, ct)
            || await _context.ImplementationArtifacts.AnyAsync(x => x.CreatedByUserId == userId || x.UpdatedByUserId == userId, ct)
            || await _context.VModelTestCases.AnyAsync(x => x.CreatedByUserId == userId || x.UpdatedByUserId == userId, ct)
            || await _context.VModelTestExecutions.AnyAsync(x => x.ExecutedByUserId == userId, ct)
            || await _context.VModelPhaseValidations.AnyAsync(x => x.ValidatedByUserId == userId, ct)
            || await _context.SrsDocuments.AnyAsync(x => x.GeneratedByUserId == userId || x.SavedByUserId == userId, ct)
            || await _context.Notifications.AnyAsync(x => x.UserId == userId, ct)
            || await _context.ActivityLogs.AnyAsync(x => x.ActorUserId == userId, ct)
            || await _context.ProgressReports.AnyAsync(x => x.CreatedByUserId == userId, ct);
    }

    private void AddAudit(
        string action,
        string targetUserId,
        string actorUserId,
        string description,
        string? oldValue = null,
        string? newValue = null)
    {
        _context.ActivityLogs.Add(new ActivityLog
        {
            ActorUserId = actorUserId,
            Action = action,
            ResourceType = nameof(ApplicationUser),
            ResourceId = targetUserId,
            Description = description,
            OldValues = oldValue,
            NewValues = newValue,
            CreatedAt = _clock.UtcNow
        });
    }

    private async Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken ct)
    {
        return _context.Database.IsRelational()
            ? await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct)
            : null;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string IdentityErrors(IdentityResult result) =>
        string.Join(" ", result.Errors.Select(error => error.Description));

    private static AdminUserOperationResult Fail(string message) =>
        AdminUserOperationResult.Failure(message);
}
