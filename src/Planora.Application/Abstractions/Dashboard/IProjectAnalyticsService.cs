using Planora.Application.Common.Dashboard;

namespace Planora.Application.Abstractions.Dashboard;

public interface IProjectAnalyticsService
{
    Task<ProjectAnalyticsSummary?>
        GetProjectAnalyticsAsync(
            int projectId,
            CancellationToken cancellationToken = default);
}