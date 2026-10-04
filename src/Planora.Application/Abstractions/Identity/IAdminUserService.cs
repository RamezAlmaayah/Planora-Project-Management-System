using Planora.Application.Common.Identity;

namespace Planora.Application.Abstractions.Identity;

public interface IAdminUserService
{
    Task<AdminUserListResult> GetUsersAsync(
        AdminUserListQuery query,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetails?> GetUserAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<AdminUserOperationResult> CreateAsync(
        CreateAdminUserRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminUserOperationResult> ChangeGlobalRoleAsync(
        ChangeAdminUserRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminUserOperationResult> DisableAsync(
        AdminUserStateRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminUserOperationResult> EnableAsync(
        AdminUserStateRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminUserOperationResult> DeleteAsync(
        AdminUserStateRequest request,
        CancellationToken cancellationToken = default);
}
