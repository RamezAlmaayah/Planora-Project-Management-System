using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Security.Claims;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Projects.Members;
using Planora.Application.Common.Security;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;
using Planora.Infrastructure.Projects;
using Planora.Infrastructure.Scrum;
using Planora.Web.Controllers;
using Planora.Web.ViewModels.Projects;

namespace Planora.IntegrationTests;

public sealed class StabilizationTests
{
    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RoleUpdateRequiresMatchingProject(bool matchingProject)
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True((await roles.CreateAsync(new IdentityRole(SystemRoles.Developer))).Succeeded);
        Assert.True((await roles.CreateAsync(new IdentityRole(SystemRoles.ScrumMaster))).Succeeded);
        var user = new ApplicationUser { UserName = "member", Email = "member@example.test", EmailConfirmed = true };
        Assert.True((await users.CreateAsync(user)).Succeeded);
        Assert.True((await users.AddToRolesAsync(user, [SystemRoles.Developer, SystemRoles.ScrumMaster])).Succeeded);
        var project = new Project { Name = "Target", Methodology = ProjectMethodology.Scrum };
        var otherProject = new Project { Name = "Authorized", Methodology = ProjectMethodology.Scrum };
        var member = new ProjectMember { Project = project, UserId = user.Id, Role = ProjectMemberRole.Developer };
        context.AddRange(member, otherProject);
        await context.SaveChangesAsync();
        var service = new ProjectMemberService(context, users, new TestClock());

        var result = await service.UpdateMemberRoleAsync(new UpdateProjectMemberRoleRequest
        {
            ProjectId = matchingProject ? project.Id : otherProject.Id,
            MembershipId = member.Id,
            Role = ProjectMemberRole.ScrumMaster
        });

        Assert.Equal(matchingProject, result.Succeeded);
        context.ChangeTracker.Clear();
        var saved = await context.ProjectMembers.SingleAsync();
        Assert.Equal(matchingProject ? ProjectMemberRole.ScrumMaster : ProjectMemberRole.Developer, saved.Role);
    }

    [Theory]
    [InlineData(ProjectStatus.Archived, false)]
    [InlineData(ProjectStatus.Active, true)]
    public async Task BacklogRemovalRespectsArchivedProject(ProjectStatus status, bool allowed)
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var project = new Project { Name = "Project", Methodology = ProjectMethodology.Scrum, Status = status };
        var sprint = new Sprint { Project = project, Name = "Sprint" };
        var backlog = new BacklogItem { Project = project, Title = "Item", Status = BacklogItemStatus.InSprint };
        var assignment = new SprintBacklogItem { Sprint = sprint, BacklogItem = backlog };
        context.Add(assignment);
        await context.SaveChangesAsync();
        var service = new SprintService(context, new TestClock());

        var result = await service.RemoveBacklogItemAsync(project.Id, sprint.Id, backlog.Id, "manager");

        Assert.Equal(allowed, result.Succeeded);
        context.ChangeTracker.Clear();
        Assert.Equal(!allowed, await context.SprintBacklogItems.AnyAsync());
        Assert.Equal(allowed ? BacklogItemStatus.Ready : BacklogItemStatus.InSprint,
            (await context.BacklogItems.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(404, "NotFound")]
    [InlineData(403, "StatusCode")]
    [InlineData(500, "StatusCode")]
    public void StatusCodeUsesExistingView(int code, string viewName)
    {
        var controller = new ErrorController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        var result = Assert.IsType<ViewResult>(controller.HandleStatusCode(code));
        Assert.Equal(viewName, result.ViewName);
        Assert.Equal(code, controller.Response.StatusCode);
    }

    [Fact]
    public void AccessDeniedReturnsForbidden()
    {
        var controller = new AccountController(null!, null!, null!, new TestClock(),
            NullLogger<AccountController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        Assert.IsType<ViewResult>(controller.AccessDenied());
        Assert.Equal(StatusCodes.Status403Forbidden, controller.Response.StatusCode);
    }

    [Theory]
    [InlineData(null, null, 2)]
    [InlineData(ProjectStatus.Active, null, 1)]
    [InlineData(ProjectStatus.Active, "missing", 0)]
    public async Task ProjectFilterCombinesStatusSearchAndMembership(ProjectStatus? status, string? search, int count)
    {
        using var services = CreateServices();
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (var projectStatus in new[] { ProjectStatus.Active, ProjectStatus.Archived })
        {
            context.Add(new ProjectMember
            {
                UserId = "member", Role = ProjectMemberRole.Developer,
                Project = new Project { Name = "Visible", Status = projectStatus }
            });
        }
        context.Add(new Project { Name = "Inaccessible", Status = ProjectStatus.Active });
        await context.SaveChangesAsync();
        var controller = new ProjectsController(new ProjectService(context, new TestClock()),
            null!, new TestClock(), NullLogger<ProjectsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "member")], "Test"))
                }
            }
        };

        var result = Assert.IsType<ViewResult>(await controller.Index(search, default, status));
        var model = Assert.IsType<ProjectIndexViewModel>(result.Model);
        Assert.Equal(count, model.Projects.Count);
        Assert.Equal(status, model.Status);
        Assert.DoesNotContain(model.Projects, project => project.Name == "Inaccessible");
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
    }
}
