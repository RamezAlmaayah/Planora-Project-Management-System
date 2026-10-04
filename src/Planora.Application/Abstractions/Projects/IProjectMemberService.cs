using Planora.Application.Common.Projects.Members;

namespace Planora.Application.Abstractions.Projects;

public interface IProjectMemberService
{
    Task<IReadOnlyList<ProjectMemberSummary>>
        GetMembersAsync(
            int projectId,
            CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AvailableProjectUser>>
        GetAvailableUsersAsync(
            int projectId,
            CancellationToken cancellationToken = default);

    Task<ProjectMemberOperationResult>
        AddMemberAsync(
            AddProjectMemberRequest request,
            CancellationToken cancellationToken = default);

    Task<ProjectMemberOperationResult>
        UpdateMemberRoleAsync(
            UpdateProjectMemberRoleRequest request,
            CancellationToken cancellationToken = default);

    Task<ProjectMemberOperationResult>
        RemoveMemberAsync(
            int projectId,
            int membershipId,
            CancellationToken cancellationToken = default);

    Task<ProjectMemberOperationResult>
        RemoveMemberAsync(
            int projectId,
            int membershipId,
            string actorUserId,
            CancellationToken cancellationToken = default);
}
