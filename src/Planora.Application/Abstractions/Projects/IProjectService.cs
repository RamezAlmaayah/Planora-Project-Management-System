using Planora.Application.Common.Projects;

namespace Planora.Application.Abstractions.Projects;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectSummary>>
        GetAccessibleProjectsAsync(
            string userId,
            bool isAdmin,
            CancellationToken cancellationToken = default);

    Task<ProjectSummary?>
        GetAccessibleProjectByIdAsync(
            int projectId,
            string userId,
            bool isAdmin,
            CancellationToken cancellationToken = default);

    Task<bool>
        ProjectExistsAsync(
            int projectId,
            CancellationToken cancellationToken = default);

    Task<bool>
        ProjectNameExistsAsync(
            string projectName,
            int? excludedProjectId = null,
            CancellationToken cancellationToken = default);

    Task<ProjectSummary>
        CreateProjectAsync(
            CreateProjectRequest request,
            CancellationToken cancellationToken = default);

    Task<ProjectSummary?>
        UpdateProjectAsync(
            UpdateProjectRequest request,
            CancellationToken cancellationToken = default);

    Task<bool>
        ArchiveProjectAsync(
            int projectId,
            string updatedByUserId,
            CancellationToken cancellationToken = default);
}