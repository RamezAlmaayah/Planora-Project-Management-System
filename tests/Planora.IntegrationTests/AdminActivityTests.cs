using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Security;
using Planora.Domain.Entities;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;
using Planora.Web.Controllers;
using Planora.Web.Presence;
using Planora.Web.ViewModels.AdminActivity;

namespace Planora.IntegrationTests;

public sealed class AdminActivityTests
{
    [Fact]
    public async Task ActivityIsAdminOnlyPagedNewestFirstAndOmitsSensitiveAuditValues()
    {
        var (provider, scope, controller) = await CreateAsync();
        await using (provider)
        await using (scope)
        {
            var attribute = typeof(AdminActivityController).GetCustomAttribute<AuthorizeAttribute>();
            Assert.Equal(AuthorizationPolicies.AdminOnly, attribute?.Policy);
            var result = Assert.IsType<ViewResult>(await controller.Index(null, null, null,
                new DateTime(2026, 10, 3), new DateTime(2026, 10, 3), page: 1));
            var model = Assert.IsType<AdminActivityViewModel>(result.Model);
            Assert.Equal(35, model.TotalCount);
            Assert.Equal(2, model.TotalPages);
            Assert.Equal(30, model.Entries.Count);
            Assert.Equal("Demo Action34", model.Entries[0].Action);
            Assert.DoesNotContain(typeof(ActivityEntry).GetProperties(), property =>
                property.Name is "OldValues" or "NewValues" or "TraceId");
        }
    }

    private static async Task<(ServiceProvider Provider, AsyncServiceScope Scope, AdminActivityController Controller)> CreateAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        string dbName = Guid.NewGuid().ToString();
        services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton<IClock>(new TestClock());
        services.AddSingleton(new UserPresenceRegistry());
        var provider = services.BuildServiceProvider();
        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Users.Add(new ApplicationUser { Id = "actor", UserName = "actor", FullName = "Demo Actor" });
            db.Projects.Add(new Project { Id = 7, Name = "Demo Project", Methodology = Planora.Domain.Enums.ProjectMethodology.Scrum,
                CreatedByUserId = "actor", CreatedAt = new DateTime(2026, 10, 3) });
            for (int i = 0; i < 35; i++)
                db.ActivityLogs.Add(new ActivityLog
                {
                    ActorUserId = "actor", ProjectId = 7, Action = $"DemoAction{i}",
                    ResourceType = "TaskItem", ResourceId = i.ToString(), Description = "Demo activity",
                    OldValues = "sensitive old value", NewValues = "sensitive new value",
                    CreatedAt = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc).AddMinutes(i)
                });
            await db.SaveChangesAsync();
        }
        var requestScope = provider.CreateAsyncScope();
        var controller = new AdminActivityController(
            requestScope.ServiceProvider.GetRequiredService<ApplicationDbContext>(),
            requestScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            requestScope.ServiceProvider.GetRequiredService<UserPresenceRegistry>(),
            requestScope.ServiceProvider.GetRequiredService<IClock>());
        controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() };
        return (provider, requestScope, controller);
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 10, 3, 15, 0, 0, DateTimeKind.Utc);
    }
}
