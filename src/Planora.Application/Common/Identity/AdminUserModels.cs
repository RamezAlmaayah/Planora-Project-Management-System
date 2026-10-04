using Planora.Domain.Enums;

namespace Planora.Application.Common.Identity;

public sealed class AdminUserListQuery
{
    public string? Search { get; set; }
    public string? Role { get; set; }
    public bool? IsDisabled { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class AdminUserListResult
{
    public IReadOnlyList<AdminUserSummary> Users { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => TotalCount == 0
        ? 1
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class AdminUserSummary
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string GlobalRole { get; set; } = string.Empty;
    public bool IsDisabled { get; set; }
    public bool EmailConfirmed { get; set; }
    public int ProjectMembershipCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool IsOnline { get; set; }
}

public sealed class AdminUserDetails : AdminUserSummary
{
    public IReadOnlyList<AdminUserProjectMembership> ProjectMemberships { get; set; } = [];
}

public sealed class AdminUserProjectMembership
{
    public int MembershipId { get; set; }
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public ProjectMethodology Methodology { get; set; }
    public ProjectStatus ProjectStatus { get; set; }
    public ProjectMemberRole ProjectRole { get; set; }
    public DateTime JoinedAt { get; set; }
}

public sealed class CreateAdminUserRequest
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string GlobalRole { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
}

public sealed class ChangeAdminUserRoleRequest
{
    public string UserId { get; set; } = string.Empty;
    public string GlobalRole { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
}

public sealed class AdminUserStateRequest
{
    public string UserId { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
}

public sealed class AdminUserOperationResult
{
    public bool Succeeded { get; private init; }
    public string? UserId { get; private init; }
    public string? ErrorMessage { get; private init; }

    public static AdminUserOperationResult Success(string userId) =>
        new() { Succeeded = true, UserId = userId };

    public static AdminUserOperationResult Failure(string message) =>
        new() { ErrorMessage = message };
}
