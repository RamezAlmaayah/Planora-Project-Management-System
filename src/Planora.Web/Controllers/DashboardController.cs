using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Dashboard;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Common.Dashboard;
using Planora.Application.Common.Projects;
using Planora.Application.Common.Security;
using Planora.Web.ViewModels.Dashboard;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    private readonly IProjectService _projectService;
    private readonly IProjectAnalyticsService _projectAnalyticsService;

    public DashboardController(
        IProjectService projectService,
        IProjectAnalyticsService projectAnalyticsService)
    {
        _projectService = projectService;
        _projectAnalyticsService = projectAnalyticsService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? projectId,
        CancellationToken cancellationToken)
    {
        string? userId =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        bool isAdmin =
            User.IsInRole(
                SystemRoles.Admin);

        IReadOnlyList<ProjectSummary> projects =
            await _projectService
                .GetAccessibleProjectsAsync(
                    userId,
                    isAdmin,
                    cancellationToken);

        ProjectSummary? selectedProject = null;

        if (projectId.HasValue)
        {
            selectedProject =
                projects.FirstOrDefault(project =>
                    project.Id == projectId.Value);

            if (selectedProject is null)
            {
                bool projectExists =
                    await _projectService
                        .ProjectExistsAsync(
                            projectId.Value,
                            cancellationToken);

                if (projectExists)
                {
                    return Forbid();
                }

                return NotFound();
            }
        }
        else
        {
            selectedProject =
                projects.FirstOrDefault();
        }

        ProjectAnalyticsSummary? analytics = null;

        if (selectedProject is not null)
        {
            analytics =
                await _projectAnalyticsService
                    .GetProjectAnalyticsAsync(
                        selectedProject.Id,
                        cancellationToken);
        }

        string userName =
            User.Identity?.Name
            ?? "Planora User";

        var model =
            new DashboardViewModel
            {
                UserName =
                    userName,

                Projects =
                    projects,

                SelectedProjectId =
                    selectedProject?.Id,

                Analytics =
                    analytics
            };

        return View(model);
    }
}