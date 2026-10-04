using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Common.Projects.Members;
using Planora.Application.Common.Security;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Projects;

public sealed class ProjectMemberService
    : IProjectMemberService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClock _clock;

    public ProjectMemberService(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IClock clock)
    {
        _context = context;
        _userManager = userManager;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ProjectMemberSummary>>
        GetMembersAsync(
            int projectId,
            CancellationToken cancellationToken = default)
    {
        return await (
            from member in _context.ProjectMembers
                .AsNoTracking()

            join user in _context.Users
                .AsNoTracking()
                on member.UserId equals user.Id

            where member.ProjectId == projectId

            orderby member.Role, user.FullName

            select new ProjectMemberSummary
            {
                Id =
                    member.Id,

                ProjectId =
                    member.ProjectId,

                UserId =
                    member.UserId,

                FullName =
                    user.FullName,

                Email =
                    user.Email ?? string.Empty,

                GlobalRole =
                    (from userRole in _context.UserRoles
                     join identityRole in _context.Roles
                         on userRole.RoleId equals identityRole.Id
                     where userRole.UserId == user.Id
                     select identityRole.Name)
                    .FirstOrDefault() ?? string.Empty,

                IsDisabled =
                    user.IsDisabled,

                Role =
                    member.Role,

                JoinedAt =
                    member.JoinedAt
            })
            .ToListAsync(
                cancellationToken);
    }

    public async Task<IReadOnlyList<AvailableProjectUser>>
        GetAvailableUsersAsync(
            int projectId,
            CancellationToken cancellationToken = default)
    {
        Project? project =
            await _context.Projects
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    project =>
                        project.Id == projectId,
                    cancellationToken);

        if (project is null)
        {
            return Array.Empty<AvailableProjectUser>();
        }

        List<string> existingUserIds =
            await _context.ProjectMembers
                .AsNoTracking()
                .Where(member =>
                    member.ProjectId == projectId)
                .Select(member =>
                    member.UserId)
                .ToListAsync(
                    cancellationToken);

        List<ApplicationUser> users =
            await _userManager.Users
                .AsNoTracking()
                .Where(user =>
                    user.EmailConfirmed
                    && !user.IsDisabled
                    && !existingUserIds.Contains(user.Id))
                .OrderBy(user =>
                    user.FullName)
                .ToListAsync(
                    cancellationToken);

        var result =
            new List<AvailableProjectUser>();

        foreach (ApplicationUser user in users)
        {
            IList<string> globalRoles =
                await _userManager.GetRolesAsync(
                    user);

            if (globalRoles.Contains(
                    SystemRoles.Admin))
            {
                continue;
            }

            IReadOnlyList<ProjectMemberRole>
                eligibleRoles =
                    GetEligibleProjectRoles(
                        globalRoles,
                        project.Methodology);

            if (eligibleRoles.Count == 0)
            {
                continue;
            }

            result.Add(
                new AvailableProjectUser
                {
                    UserId =
                        user.Id,

                    FullName =
                        string.IsNullOrWhiteSpace(
                            user.FullName)
                            ? user.Email ?? "User"
                            : user.FullName,

                    Email =
                        user.Email ?? string.Empty,

                    EligibleProjectRoles =
                        eligibleRoles
                });
        }

        return result;
    }

    public async Task<ProjectMemberOperationResult>
        AddMemberAsync(
            AddProjectMemberRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(
                typeof(ProjectMemberRole),
                request.Role))
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Invalid project role.");
        }

        Project? project =
            await _context.Projects
                .FirstOrDefaultAsync(
                    project =>
                        project.Id ==
                        request.ProjectId,
                    cancellationToken);

        if (project is null)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Project was not found.");
        }

        if (project.Status ==
            ProjectStatus.Archived)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Members cannot be added to an archived project.");
        }

        ApplicationUser? user =
            await _userManager.FindByIdAsync(
                request.UserId);

        if (user is null)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "User was not found.");
        }

        if (!user.EmailConfirmed)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Only confirmed users can be added to projects.");
        }

        if (user.IsDisabled)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Disabled users cannot be added to projects.");
        }

        bool alreadyMember =
            await _context.ProjectMembers
                .AnyAsync(
                    member =>
                        member.ProjectId ==
                            request.ProjectId
                        && member.UserId ==
                            request.UserId,
                    cancellationToken);

        if (alreadyMember)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "User is already a member of this project.");
        }

        IList<string> globalRoles =
            await _userManager.GetRolesAsync(
                user);

        if (globalRoles.Contains(
                SystemRoles.Admin))
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Administrators do not require project membership.");
        }

        bool roleAllowed =
            IsProjectRoleAllowed(
                globalRoles,
                request.Role,
                project.Methodology);

        if (!roleAllowed)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "The selected project role does not match the user's system role or project methodology.");
        }

        var member =
            new ProjectMember
            {
                ProjectId =
                    request.ProjectId,

                UserId =
                    request.UserId,

                Role =
                    request.Role,

                JoinedAt =
                    _clock.UtcNow
            };

        _context.ProjectMembers.Add(
            member);

        AddAudit(
            "ProjectMemberAdded",
            member.UserId,
            request.ActorUserId,
            member.ProjectId,
            $"User {member.UserId} was added to project {member.ProjectId} as {member.Role}.");

        try
        {
            await _context.SaveChangesAsync(
                cancellationToken);
        }
        catch (DbUpdateException)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "User is already a member of this project.");
        }

        return ProjectMemberOperationResult
            .Success(
                member.Id);
    }

    public async Task<ProjectMemberOperationResult>
        UpdateMemberRoleAsync(
            UpdateProjectMemberRoleRequest request,
            CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(
                typeof(ProjectMemberRole),
                request.Role))
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Invalid project role.");
        }

        ProjectMember? member =
            await _context.ProjectMembers
                .FirstOrDefaultAsync(
                    member =>
                        member.Id ==
                        request.MembershipId &&
                        member.ProjectId == request.ProjectId,
                    cancellationToken);

        if (member is null)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Project member was not found.");
        }

        Project? project =
            await _context.Projects
                .FirstOrDefaultAsync(
                    project =>
                        project.Id ==
                        member.ProjectId,
                    cancellationToken);

        if (project is null)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Project was not found.");
        }

        if (project.Status ==
            ProjectStatus.Archived)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Member roles cannot be changed in an archived project.");
        }

        ApplicationUser? user =
            await _userManager.FindByIdAsync(
                member.UserId);

        if (user is null)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "User was not found.");
        }

        IList<string> globalRoles =
            await _userManager.GetRolesAsync(
                user);

        bool roleAllowed =
            IsProjectRoleAllowed(
                globalRoles,
                request.Role,
                project.Methodology);

        if (!roleAllowed)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "The selected project role does not match the user's system role or project methodology.");
        }

        ProjectMemberRole oldRole = member.Role;

        member.Role = request.Role;

        AddAudit(
            "ProjectMemberRoleChanged",
            member.UserId,
            request.ActorUserId,
            member.ProjectId,
            $"Project role changed for membership {member.Id}.",
            oldRole.ToString(),
            request.Role.ToString());

        await _context.SaveChangesAsync(
            cancellationToken);

        return ProjectMemberOperationResult
            .Success(
                member.Id);
    }

    public async Task<ProjectMemberOperationResult>
        RemoveMemberAsync(
            int projectId,
            int membershipId,
            CancellationToken cancellationToken = default)
    {
        return await RemoveMemberAsync(
            projectId,
            membershipId,
            string.Empty,
            cancellationToken);
    }

    public async Task<ProjectMemberOperationResult>
        RemoveMemberAsync(
            int projectId,
            int membershipId,
            string actorUserId,
            CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction? transaction =
            _context.Database.IsRelational()
                ? await _context.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken)
                : null;

        Project? project =
            await _context.Projects
                .FirstOrDefaultAsync(
                    project =>
                        project.Id == projectId,
                    cancellationToken);

        if (project is null)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Project was not found.");
        }

        if (project.Status ==
            ProjectStatus.Archived)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Members cannot be removed from an archived project.");
        }

        ProjectMember? member =
            await _context.ProjectMembers
                .FirstOrDefaultAsync(
                    member =>
                        member.Id ==
                            membershipId
                        && member.ProjectId ==
                            projectId,
                    cancellationToken);

        if (member is null)
        {
            return ProjectMemberOperationResult
                .Failure(
                    "Project member was not found.");
        }

        int activeTaskCount = await _context.TaskItems
            .CountAsync(task =>
                task.AssignedUserId == member.UserId &&
                task.Status != TaskItemStatus.Done &&
                task.SprintBacklogItem.Sprint.ProjectId == projectId,
                cancellationToken);

        int activeIssueCount = await _context.Issues
            .CountAsync(issue =>
                issue.ProjectId == projectId &&
                issue.AssignedUserId == member.UserId &&
                issue.Status != IssueStatus.Closed,
                cancellationToken);

        if (activeTaskCount > 0 || activeIssueCount > 0)
        {
            string taskText = $"{activeTaskCount} active task{(activeTaskCount == 1 ? string.Empty : "s")}";
            string issueText = $"{activeIssueCount} open issue{(activeIssueCount == 1 ? string.Empty : "s")}";
            string responsibilities = activeTaskCount > 0 && activeIssueCount > 0
                ? $"{taskText} and {issueText}"
                : activeTaskCount > 0 ? taskText : issueText;

            return ProjectMemberOperationResult.Failure(
                $"User cannot be removed because they are assigned to {responsibilities}.");
        }

        _context.ProjectMembers.Remove(
            member);

        AddAudit(
            "ProjectMemberRemoved",
            member.UserId,
            actorUserId,
            projectId,
            $"Membership {membershipId} was removed from project {projectId}.");

        await _context.SaveChangesAsync(
            cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return ProjectMemberOperationResult
            .Success(
                membershipId);
    }

    private static IReadOnlyList<ProjectMemberRole>
        GetEligibleProjectRoles(
            IEnumerable<string> globalRoles,
            ProjectMethodology methodology)
    {
        var roles =
            new List<ProjectMemberRole>();

        if (globalRoles.Contains(
                SystemRoles.ProjectManager))
        {
            roles.Add(
                ProjectMemberRole.ProjectManager);
        }

        if (globalRoles.Contains(
                SystemRoles.ScrumMaster)
            && methodology ==
                ProjectMethodology.Scrum)
        {
            roles.Add(
                ProjectMemberRole.ScrumMaster);
        }

        if (globalRoles.Contains(
                SystemRoles.Developer))
        {
            roles.Add(
                ProjectMemberRole.Developer);
        }

        if (globalRoles.Contains(
                SystemRoles.QaTester))
        {
            roles.Add(
                ProjectMemberRole.QaTester);
        }

        return roles;
    }

    private static bool
        IsProjectRoleAllowed(
            IEnumerable<string> globalRoles,
            ProjectMemberRole projectRole,
            ProjectMethodology methodology)
    {
        return projectRole switch
        {
            ProjectMemberRole.ProjectManager =>
                globalRoles.Contains(
                    SystemRoles.ProjectManager),

            ProjectMemberRole.ScrumMaster =>
                methodology ==
                    ProjectMethodology.Scrum
                && globalRoles.Contains(
                    SystemRoles.ScrumMaster),

            ProjectMemberRole.Developer =>
                globalRoles.Contains(
                    SystemRoles.Developer),

            ProjectMemberRole.QaTester =>
                globalRoles.Contains(
                    SystemRoles.QaTester),

            _ =>
                false
        };
    }

    private void AddAudit(
        string action,
        string targetUserId,
        string actorUserId,
        int projectId,
        string description,
        string? oldValue = null,
        string? newValue = null)
    {
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return;
        }

        _context.ActivityLogs.Add(new ActivityLog
        {
            ActorUserId = actorUserId,
            ProjectId = projectId,
            Action = action,
            ResourceType = nameof(ProjectMember),
            ResourceId = targetUserId,
            Description = description,
            OldValues = oldValue,
            NewValues = newValue,
            CreatedAt = _clock.UtcNow
        });
    }
}
