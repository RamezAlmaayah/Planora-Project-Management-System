using Planora.Domain.Enums;

namespace Planora.Application.Abstractions.Security;

public interface IProjectAccessService
{
    Task<bool> IsProjectMemberAsync(
        int projectId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<ProjectMemberRole?> GetProjectRoleAsync(
        int projectId,
        string userId,
        CancellationToken cancellationToken = default);
}