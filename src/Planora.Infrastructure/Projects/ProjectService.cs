using Microsoft.EntityFrameworkCore;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Common.Projects;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Projects;

public sealed class ProjectService : IProjectService
{
    private readonly ApplicationDbContext _context;
    private readonly IClock _clock;

    public ProjectService(
        ApplicationDbContext context,
        IClock clock)
    {
        _context = context;
        _clock = clock;
    }

    public async Task<IReadOnlyList<ProjectSummary>>
        GetAccessibleProjectsAsync(
            string userId,
            bool isAdmin,
            CancellationToken cancellationToken = default)
    {
        var query =
            _context.Projects
                .AsNoTracking()
                .AsQueryable();

        if (!isAdmin)
        {
            query =
                query.Where(project =>
                    _context.ProjectMembers.Any(member =>
                        member.ProjectId == project.Id
                        && member.UserId == userId));
        }

        return await query
            .OrderByDescending(project =>
                project.CreatedAt)
            .Select(project =>
                new ProjectSummary
                {
                    Id =
                        project.Id,

                    Name =
                        project.Name,

                    Description =
                        project.Description,

                    Objectives =
                        project.Objectives,

                    Scope =
                        project.Scope,

                    Methodology =
                        project.Methodology,

                    Status =
                        project.Status,

                    StartDate =
                        project.StartDate,

                    EndDate =
                        project.EndDate,

                    MemberCount =
                        _context.ProjectMembers.Count(member =>
                            member.ProjectId == project.Id),

                    CreatedAt =
                        project.CreatedAt
                })
            .ToListAsync(
                cancellationToken);
    }

    public async Task<ProjectSummary?>
        GetAccessibleProjectByIdAsync(
            int projectId,
            string userId,
            bool isAdmin,
            CancellationToken cancellationToken = default)
    {
        var query =
            _context.Projects
                .AsNoTracking()
                .Where(project =>
                    project.Id == projectId);

        if (!isAdmin)
        {
            query =
                query.Where(project =>
                    _context.ProjectMembers.Any(member =>
                        member.ProjectId == project.Id
                        && member.UserId == userId));
        }

        return await query
            .Select(project =>
                new ProjectSummary
                {
                    Id =
                        project.Id,

                    Name =
                        project.Name,

                    Description =
                        project.Description,

                    Objectives =
                        project.Objectives,

                    Scope =
                        project.Scope,

                    Methodology =
                        project.Methodology,

                    Status =
                        project.Status,

                    StartDate =
                        project.StartDate,

                    EndDate =
                        project.EndDate,

                    MemberCount =
                        _context.ProjectMembers.Count(member =>
                            member.ProjectId == project.Id),

                    CreatedAt =
                        project.CreatedAt
                })
            .FirstOrDefaultAsync(
                cancellationToken);
    }

    public Task<bool>
        ProjectExistsAsync(
            int projectId,
            CancellationToken cancellationToken = default)
    {
        return _context.Projects
            .AsNoTracking()
            .AnyAsync(
                project =>
                    project.Id == projectId,
                cancellationToken);
    }

    public async Task<bool>
        ProjectNameExistsAsync(
            string projectName,
            int? excludedProjectId = null,
            CancellationToken cancellationToken = default)
    {
        string normalizedName =
            projectName.Trim();

        var query =
            _context.Projects
                .AsNoTracking()
                .Where(project =>
                    project.Name == normalizedName);

        if (excludedProjectId.HasValue)
        {
            query =
                query.Where(project =>
                    project.Id
                    != excludedProjectId.Value);
        }

        return await query
            .AnyAsync(
                cancellationToken);
    }

    public async Task<ProjectSummary>
        CreateProjectAsync(
            CreateProjectRequest request,
            CancellationToken cancellationToken = default)
    {
        var project =
            new Project
            {
                Name =
                    request.Name.Trim(),

                Description =
                    request.Description.Trim(),

                Objectives =
                    string.Empty,

                Scope =
                    string.Empty,

                Methodology =
                    request.Methodology,

                Status =
                    ProjectStatus.Planning,

                StartDate =
                    request.StartDate.Date,

                EndDate =
                    request.EndDate?.Date,

                CreatedByUserId =
                    request.CreatedByUserId,

                CreatedAt =
                    _clock.UtcNow
            };

        _context.Projects.Add(
            project);

        if (request.Methodology == ProjectMethodology.VModel)
        {
            foreach ((VModelPhaseType phaseType, int index) in
                     Planora.Domain.VModelPhaseCatalog.Ordered.Select((phase, index) => (phase, index)))
            {
                project.VModelPhases.Add(new VModelPhase
                {
                    PhaseType = phaseType,
                    PhaseOrder = index + 1,
                    Status = VModelPhaseStatus.NotStarted,
                    CreatedAt = _clock.UtcNow
                });
            }
        }

        if (request.AddCreatorAsProjectManager)
        {
            var membership =
                new ProjectMember
                {
                    Project =
                        project,

                    UserId =
                        request.CreatedByUserId,

                    Role =
                        ProjectMemberRole.ProjectManager,

                    JoinedAt =
                        _clock.UtcNow
                };

            _context.ProjectMembers.Add(
                membership);
        }

        await _context.SaveChangesAsync(
            cancellationToken);

        int memberCount =
            request.AddCreatorAsProjectManager
                ? 1
                : 0;

        return ToSummary(
            project,
            memberCount);
    }

    public async Task<ProjectSummary?>
        UpdateProjectAsync(
            UpdateProjectRequest request,
            CancellationToken cancellationToken = default)
    {
        Project? project =
            await _context.Projects
                .FirstOrDefaultAsync(
                    project =>
                        project.Id == request.Id,
                    cancellationToken);

        if (project is null)
        {
            return null;
        }

        project.Name =
            request.Name.Trim();

        project.Description =
            request.Description.Trim();

        project.Methodology =
            request.Methodology;

        project.Status =
            request.Status;

        project.StartDate =
            request.StartDate.Date;

        project.EndDate =
            request.EndDate?.Date;

        project.UpdatedByUserId =
            request.UpdatedByUserId;

        project.UpdatedAt =
            _clock.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        int memberCount =
            await _context.ProjectMembers
                .CountAsync(
                    member =>
                        member.ProjectId == project.Id,
                    cancellationToken);

        return ToSummary(
            project,
            memberCount);
    }

    public async Task<bool>
        ArchiveProjectAsync(
            int projectId,
            string updatedByUserId,
            CancellationToken cancellationToken = default)
    {
        Project? project =
            await _context.Projects
                .FirstOrDefaultAsync(
                    project =>
                        project.Id == projectId,
                    cancellationToken);

        if (project is null)
        {
            return false;
        }

        if (project.Status ==
            ProjectStatus.Archived)
        {
            return true;
        }

        project.Status =
            ProjectStatus.Archived;

        project.UpdatedByUserId =
            updatedByUserId;

        project.UpdatedAt =
            _clock.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return true;
    }

    private static ProjectSummary
        ToSummary(
            Project project,
            int memberCount)
    {
        return new ProjectSummary
        {
            Id =
                project.Id,

            Name =
                project.Name,

            Description =
                project.Description,

            Objectives =
                project.Objectives,

            Scope =
                project.Scope,

            Methodology =
                project.Methodology,

            Status =
                project.Status,

            StartDate =
                project.StartDate,

            EndDate =
                project.EndDate,

            MemberCount =
                memberCount,

            CreatedAt =
                project.CreatedAt
        };
    }
}
