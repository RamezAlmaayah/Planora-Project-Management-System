using Microsoft.EntityFrameworkCore;
using Planora.Application.Abstractions.Security;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Security;

public sealed class ProjectAccessService : IProjectAccessService
{
    private readonly ApplicationDbContext _dbContext;

    public ProjectAccessService(
        ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> IsProjectMemberAsync(
        int projectId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProjectMembers
            .AsNoTracking()
            .AnyAsync(
                x => x.ProjectId == projectId
                     && x.UserId == userId,
                cancellationToken);
    }

    public async Task<ProjectMemberRole?> GetProjectRoleAsync(
        int projectId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.ProjectMembers
            .AsNoTracking()
            .Where(
                x => x.ProjectId == projectId
                     && x.UserId == userId)
            .Select(
                x => (ProjectMemberRole?)x.Role)
            .FirstOrDefaultAsync(cancellationToken);
    }
}