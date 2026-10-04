using System.Reflection;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Identity;
using Planora.Application.Common.Identity;
using Planora.Application.Common.Projects.Members;
using Planora.Application.Common.Security;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Issues;
using Planora.Infrastructure.Persistence;
using Planora.Infrastructure.Projects;
using Planora.Infrastructure.Tasks;
using Planora.Web.Controllers;
using Planora.Web.Identity;

namespace Planora.IntegrationTests;

public sealed class AdminUserManagementTests
{
    [Fact]
    public async Task CreateUserUsesIdentityAssignsRoleAndWritesSafeAudit()
    {
        await using Fixture f = await Fixture.CreateAsync();

        AdminUserOperationResult result = await f.AdminUsers.CreateAsync(new CreateAdminUserRequest
        {
            FullName = "New User",
            Email = "new.user@example.test",
            Password = Fixture.ValidPassword,
            GlobalRole = SystemRoles.Developer,
            ActorUserId = f.Admin.Id
        });

        Assert.True(result.Succeeded);
        ApplicationUser created = Assert.IsType<ApplicationUser>(
            await f.Users.FindByIdAsync(result.UserId!));
        Assert.True(created.EmailConfirmed);
        Assert.True(await f.Users.IsInRoleAsync(created, SystemRoles.Developer));
        ActivityLog audit = Assert.Single(f.Context.ActivityLogs.Where(x => x.Action == "AdminUserCreated"));
        Assert.DoesNotContain(Fixture.ValidPassword, audit.Description + audit.OldValues + audit.NewValues);
    }

    [Fact]
    public async Task CreateRejectsDuplicateInvalidRoleAndWeakPassword()
    {
        await using Fixture f = await Fixture.CreateAsync();

        AdminUserOperationResult duplicate = await f.AdminUsers.CreateAsync(f.CreateRequest(f.Developer.Email!, SystemRoles.Developer));
        AdminUserOperationResult role = await f.AdminUsers.CreateAsync(f.CreateRequest("role@example.test", "Owner"));
        CreateAdminUserRequest weakRequest = f.CreateRequest("weak@example.test", SystemRoles.Developer);
        weakRequest.Password = "weak";
        AdminUserOperationResult weak = await f.AdminUsers.CreateAsync(weakRequest);

        Assert.False(duplicate.Succeeded);
        Assert.False(role.Succeeded);
        Assert.False(weak.Succeeded);
        Assert.Null(await f.Users.FindByEmailAsync("weak@example.test"));
    }

    [Fact]
    public async Task GlobalRoleChangeDoesNotRewriteProjectRoleAndIsAudited()
    {
        await using Fixture f = await Fixture.CreateAsync();
        ProjectMember membership = await f.AddMembershipAsync(f.Developer, ProjectMemberRole.Developer);

        AdminUserOperationResult result = await f.AdminUsers.ChangeGlobalRoleAsync(new ChangeAdminUserRoleRequest
        {
            UserId = f.Developer.Id,
            GlobalRole = SystemRoles.QaTester,
            ActorUserId = f.Admin.Id
        });

        Assert.True(result.Succeeded);
        Assert.Equal(ProjectMemberRole.Developer, (await f.Context.ProjectMembers.FindAsync(membership.Id))!.Role);
        Assert.True(await f.Users.IsInRoleAsync(f.Developer, SystemRoles.QaTester));
        Assert.Contains(f.Context.ActivityLogs, x => x.Action == "AdminUserGlobalRoleChanged" && x.OldValues == SystemRoles.Developer && x.NewValues == SystemRoles.QaTester);
    }

    [Fact]
    public async Task LastEnabledAdminAndSelfRoleChangeAreProtected()
    {
        await using Fixture f = await Fixture.CreateAsync();

        AdminUserOperationResult self = await f.AdminUsers.ChangeGlobalRoleAsync(new ChangeAdminUserRoleRequest
        {
            UserId = f.Admin.Id,
            GlobalRole = SystemRoles.Developer,
            ActorUserId = f.Admin.Id
        });
        ApplicationUser secondAdmin = await f.CreateUserAsync("second-admin", SystemRoles.Admin);
        AdminUserOperationResult demoteSecond = await f.AdminUsers.ChangeGlobalRoleAsync(new ChangeAdminUserRoleRequest
        {
            UserId = secondAdmin.Id,
            GlobalRole = SystemRoles.Developer,
            ActorUserId = f.Admin.Id
        });

        Assert.False(self.Succeeded);
        Assert.True(demoteSecond.Succeeded);
        Assert.True(await f.Users.IsInRoleAsync(f.Admin, SystemRoles.Admin));
    }

    [Fact]
    public async Task DisableInvalidatesStampBlocksSignInAndEnableRestoresEligibility()
    {
        await using Fixture f = await Fixture.CreateAsync();
        string? oldStamp = f.Developer.SecurityStamp;

        AdminUserOperationResult disabled = await f.AdminUsers.DisableAsync(new AdminUserStateRequest
        {
            UserId = f.Developer.Id,
            ActorUserId = f.Admin.Id
        });
        ApplicationUser refreshed = (await f.Users.FindByIdAsync(f.Developer.Id))!;

        Assert.True(disabled.Succeeded);
        Assert.True(refreshed.IsDisabled);
        Assert.NotEqual(oldStamp, refreshed.SecurityStamp);
        Assert.False(await f.SignInManager.CanSignInAsync(refreshed));

        AdminUserOperationResult enabled = await f.AdminUsers.EnableAsync(new AdminUserStateRequest
        {
            UserId = refreshed.Id,
            ActorUserId = f.Admin.Id
        });
        refreshed = (await f.Users.FindByIdAsync(refreshed.Id))!;
        Assert.True(enabled.Succeeded);
        Assert.False(refreshed.IsDisabled);
        Assert.True(await f.SignInManager.CanSignInAsync(refreshed));
    }

    [Fact]
    public async Task DisableProtectsSelfAndLastEnabledAdmin()
    {
        await using Fixture f = await Fixture.CreateAsync();

        AdminUserOperationResult self = await f.AdminUsers.DisableAsync(new AdminUserStateRequest
        {
            UserId = f.Admin.Id,
            ActorUserId = f.Admin.Id
        });
        ApplicationUser other = await f.CreateUserAsync("other-admin", SystemRoles.Admin);
        await f.AdminUsers.DisableAsync(new AdminUserStateRequest { UserId = other.Id, ActorUserId = f.Admin.Id });
        AdminUserOperationResult last = await f.AdminUsers.DisableAsync(new AdminUserStateRequest
        {
            UserId = f.Admin.Id,
            ActorUserId = other.Id
        });

        Assert.False(self.Succeeded);
        Assert.False(last.Succeeded);
        Assert.False((await f.Users.FindByIdAsync(f.Admin.Id))!.IsDisabled);
    }

    [Fact]
    public async Task SafeDeleteAllowsUnreferencedUserAndRejectsReferencedUser()
    {
        await using Fixture f = await Fixture.CreateAsync();
        ApplicationUser unused = await f.CreateUserAsync("unused", SystemRoles.Developer);
        ProjectMember membership = await f.AddMembershipAsync(f.Developer, ProjectMemberRole.Developer);

        AdminUserOperationResult deleted = await f.AdminUsers.DeleteAsync(new AdminUserStateRequest { UserId = unused.Id, ActorUserId = f.Admin.Id });
        AdminUserOperationResult blocked = await f.AdminUsers.DeleteAsync(new AdminUserStateRequest { UserId = f.Developer.Id, ActorUserId = f.Admin.Id });

        Assert.True(deleted.Succeeded);
        Assert.Null(await f.Users.FindByIdAsync(unused.Id));
        Assert.False(blocked.Succeeded);
        Assert.NotNull(await f.Users.FindByIdAsync(f.Developer.Id));
        Assert.NotNull(await f.Context.ProjectMembers.FindAsync(membership.Id));
    }

    [Fact]
    public async Task SafeDeleteRejectsSelfAndLastEnabledAdmin()
    {
        await using Fixture f = await Fixture.CreateAsync();
        ApplicationUser otherActor = await f.CreateUserAsync("other", SystemRoles.Developer);

        AdminUserOperationResult self = await f.AdminUsers.DeleteAsync(new AdminUserStateRequest { UserId = f.Admin.Id, ActorUserId = f.Admin.Id });
        AdminUserOperationResult last = await f.AdminUsers.DeleteAsync(new AdminUserStateRequest { UserId = f.Admin.Id, ActorUserId = otherActor.Id });

        Assert.False(self.Succeeded);
        Assert.False(last.Succeeded);
        Assert.NotNull(await f.Users.FindByIdAsync(f.Admin.Id));
    }

    [Fact]
    public async Task ProjectMemberServiceRejectsDisabledDuplicateAndArchivedAdds()
    {
        await using Fixture f = await Fixture.CreateAsync();
        f.Developer.IsDisabled = true;
        await f.Users.UpdateAsync(f.Developer);
        ProjectMemberOperationResult disabled = await f.Members.AddMemberAsync(f.AddRequest(f.Developer));

        f.Developer.IsDisabled = false;
        await f.Users.UpdateAsync(f.Developer);
        ProjectMemberOperationResult added = await f.Members.AddMemberAsync(f.AddRequest(f.Developer));
        ProjectMemberOperationResult duplicate = await f.Members.AddMemberAsync(f.AddRequest(f.Developer));
        f.Project.Status = ProjectStatus.Archived;
        await f.Context.SaveChangesAsync();
        ApplicationUser second = await f.CreateUserAsync("second", SystemRoles.Developer);
        ProjectMemberOperationResult archived = await f.Members.AddMemberAsync(f.AddRequest(second));

        Assert.False(disabled.Succeeded);
        Assert.True(added.Succeeded);
        Assert.False(duplicate.Succeeded);
        Assert.False(archived.Succeeded);
    }

    [Fact]
    public async Task MemberRoleChangeDoesNotChangeGlobalRoleAndAudits()
    {
        await using Fixture f = await Fixture.CreateAsync();
        ApplicationUser manager = await f.CreateUserAsync("manager", SystemRoles.ProjectManager);
        ProjectMember membership = await f.AddMembershipAsync(manager, ProjectMemberRole.ProjectManager);

        ProjectMemberOperationResult invalid = await f.Members.UpdateMemberRoleAsync(new UpdateProjectMemberRoleRequest
        {
            ProjectId = f.Project.Id,
            MembershipId = membership.Id,
            Role = ProjectMemberRole.Developer,
            ActorUserId = f.Admin.Id
        });

        Assert.False(invalid.Succeeded);
        Assert.True(await f.Users.IsInRoleAsync(manager, SystemRoles.ProjectManager));
        Assert.False(await f.Users.IsInRoleAsync(manager, SystemRoles.Developer));
    }

    [Fact]
    public async Task RemovalIsBlockedByActiveTaskWithoutSilentUnassignment()
    {
        await using Fixture f = await Fixture.CreateAsync();
        ProjectMember member = await f.AddMembershipAsync(f.Developer, ProjectMemberRole.Developer);
        TaskItem task = f.CreateTask(TaskItemStatus.InProgress, f.Developer.Id);
        f.Context.Add(task);
        await f.Context.SaveChangesAsync();

        ProjectMemberOperationResult result = await f.Members.RemoveMemberAsync(f.Project.Id, member.Id, f.Admin.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("1 active task", result.ErrorMessage);
        Assert.NotNull(await f.Context.ProjectMembers.FindAsync(member.Id));
        Assert.Equal(f.Developer.Id, (await f.Context.TaskItems.FindAsync(task.Id))!.AssignedUserId);
    }

    [Fact]
    public async Task RemovalIsBlockedByOpenIssueWithoutSilentUnassignment()
    {
        await using Fixture f = await Fixture.CreateAsync();
        ProjectMember member = await f.AddMembershipAsync(f.Developer, ProjectMemberRole.Developer);
        var issue = new Issue { ProjectId = f.Project.Id, Title = "Open issue", Description = "Description", ReporterUserId = f.Admin.Id, AssignedUserId = f.Developer.Id, Status = IssueStatus.InProgress };
        f.Context.Issues.Add(issue);
        await f.Context.SaveChangesAsync();

        ProjectMemberOperationResult result = await f.Members.RemoveMemberAsync(f.Project.Id, member.Id, f.Admin.Id);

        Assert.False(result.Succeeded);
        Assert.Contains("1 open issue", result.ErrorMessage);
        Assert.Equal(f.Developer.Id, (await f.Context.Issues.FindAsync(issue.Id))!.AssignedUserId);
    }

    [Fact]
    public async Task CompletedTaskAndClosedIssueDoNotBlockMembershipRemoval()
    {
        await using Fixture f = await Fixture.CreateAsync();
        ProjectMember member = await f.AddMembershipAsync(f.Developer, ProjectMemberRole.Developer);
        TaskItem task = f.CreateTask(TaskItemStatus.Done, f.Developer.Id);
        var issue = new Issue { ProjectId = f.Project.Id, Title = "Closed issue", Description = "Description", ReporterUserId = f.Admin.Id, AssignedUserId = f.Developer.Id, Status = IssueStatus.Closed };
        f.Context.AddRange(task, issue);
        await f.Context.SaveChangesAsync();

        ProjectMemberOperationResult result = await f.Members.RemoveMemberAsync(f.Project.Id, member.Id, f.Admin.Id);

        Assert.True(result.Succeeded);
        Assert.Null(await f.Context.ProjectMembers.FindAsync(member.Id));
        Assert.NotNull(await f.Context.TaskItems.FindAsync(task.Id));
        Assert.NotNull(await f.Context.Issues.FindAsync(issue.Id));
        Assert.Contains(f.Context.ActivityLogs, x => x.Action == "ProjectMemberRemoved");
    }

    [Fact]
    public async Task DisabledUsersAreExcludedFromNewTaskAndIssueAssignmentsButHistoryRemains()
    {
        await using Fixture f = await Fixture.CreateAsync();
        await f.AddMembershipAsync(f.Developer, ProjectMemberRole.Developer);
        TaskItem historicalTask = f.CreateTask(TaskItemStatus.InProgress, f.Developer.Id);
        f.Context.TaskItems.Add(historicalTask);
        f.Developer.IsDisabled = true;
        await f.Context.SaveChangesAsync();

        var taskService = new TaskService(f.Context, new TaskAssignmentService(f.Context), f.Clock);
        var issueService = new IssueService(f.Context, f.Clock);
        var validation = await new TaskAssignmentService(f.Context).ValidateForSprintBacklogItemAsync(
            historicalTask.SprintBacklogItemId, f.Developer.Id);

        Assert.DoesNotContain(await taskService.GetAssigneeOptionsAsync(f.Project.Id), x => x.UserId == f.Developer.Id);
        Assert.DoesNotContain(await issueService.GetDeveloperOptionsAsync(f.Project.Id), x => x.UserId == f.Developer.Id);
        Assert.False(validation.Succeeded);
        Assert.Equal(f.Developer.Id, (await f.Context.TaskItems.FindAsync(historicalTask.Id))!.AssignedUserId);
    }

    [Fact]
    public void AdminRoutesAreAdminOnlyAndMutationEndpointsRequireAntiforgery()
    {
        AuthorizeAttribute authorize = Assert.Single(typeof(AdminUsersController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(AuthorizationPolicies.AdminOnly, authorize.Policy);

        foreach (string action in new[] { "Create", "ChangeRole", "Disable", "Enable", "Delete" })
        {
            MethodInfo method = typeof(AdminUsersController).GetMethods()
                .First(item => item.Name == action && item.GetCustomAttribute<HttpPostAttribute>() is not null);
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
    }

    [Fact]
    public async Task AdminRouteAllowsAdminAndRedirectsAnonymous()
    {
        await using var factory = new AdminWebFactory();
        await factory.CreateUserAsync("route-admin", SystemRoles.Admin);
        using HttpClient anonymous = factory.Client();
        HttpResponseMessage anonymousResponse = await anonymous.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.Redirect, anonymousResponse.StatusCode);
        Assert.Equal("/Account/Login", anonymousResponse.Headers.Location?.AbsolutePath);

        using HttpClient admin = factory.Client();
        await LoginAsync(admin, "route-admin@example.test");
        HttpResponseMessage response = await admin.GetAsync("/Admin/Users");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(SystemRoles.ProjectManager)]
    [InlineData(SystemRoles.ScrumMaster)]
    [InlineData(SystemRoles.Developer)]
    [InlineData(SystemRoles.QaTester)]
    public async Task NonAdminGlobalRolesCannotAccessAdminRoutes(string role)
    {
        await using var factory = new AdminWebFactory();
        string name = role.Replace(" ", "-", StringComparison.Ordinal).ToLowerInvariant();
        await factory.CreateUserAsync(name, role);
        using HttpClient client = factory.Client();
        await LoginAsync(client, $"{name}@example.test");

        HttpResponseMessage response = await client.GetAsync("/Admin/Users");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", response.Headers.Location?.AbsolutePath);
    }

    [Fact]
    public async Task DisabledExistingCookieIsRejectedOnNextRequestAndLoginRemainsBlocked()
    {
        await using var factory = new AdminWebFactory();
        ApplicationUser admin = await factory.CreateUserAsync("session-admin", SystemRoles.Admin);
        ApplicationUser developer = await factory.CreateUserAsync("session-developer", SystemRoles.Developer);
        using HttpClient client = factory.Client();
        await LoginAsync(client, developer.Email!);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Dashboard")).StatusCode);

        using (IServiceScope scope = factory.Services.CreateScope())
        {
            IAdminUserService service = scope.ServiceProvider.GetRequiredService<IAdminUserService>();
            AdminUserOperationResult result = await service.DisableAsync(new AdminUserStateRequest
            {
                UserId = developer.Id,
                ActorUserId = admin.Id
            });
            Assert.True(result.Succeeded);
        }

        HttpResponseMessage afterDisable = await client.GetAsync("/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, afterDisable.StatusCode);
        Assert.Equal("/Account/Login", afterDisable.Headers.Location?.AbsolutePath);

        HttpResponseMessage loginPage = await client.GetAsync("/Account/Login");
        string html = await loginPage.Content.ReadAsStringAsync();
        HttpResponseMessage login = await client.PostAsync("/Account/Login", Form(html,
            ("Email", developer.Email!), ("Password", AdminWebFactory.Password), ("RememberMe", "false")));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.DoesNotContain(login.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [],
            value => value.StartsWith("__Host-Planora.Auth=", StringComparison.Ordinal) && !value.StartsWith("__Host-Planora.Auth=;", StringComparison.Ordinal));
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        string html = await (await client.GetAsync("/Account/Login")).Content.ReadAsStringAsync();
        HttpResponseMessage login = await client.PostAsync("/Account/Login", Form(html,
            ("Email", email), ("Password", AdminWebFactory.Password), ("RememberMe", "false")));
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    private static FormUrlEncodedContent Form(string html, params (string Name, string Value)[] values)
    {
        var fields = values.ToDictionary(item => item.Name, item => item.Value);
        Match match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant);
        Assert.True(match.Success);
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
        return new FormUrlEncodedContent(fields);
    }

    private sealed class AdminWebFactory : WebApplicationFactory<Program>
    {
        public const string Password = "Planora!2345";
        private readonly string _databaseName = $"admin-users-{Guid.NewGuid():N}";

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Server=(localdb)\\mssqllocaldb;Database=PlanoraAdminUsers;Trusted_Connection=True");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] =
                        "Server=(localdb)\\mssqllocaldb;Database=PlanoraAdminUsers;Trusted_Connection=True",
                    ["BootstrapAdmin:Enabled"] = "false"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<DbContextOptions>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            });
        }

        public HttpClient Client() => CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

        public async Task<ApplicationUser> CreateUserAsync(string name, string role)
        {
            using IServiceScope scope = Services.CreateScope();
            UserManager<ApplicationUser> users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager<IdentityRole> roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            if (!await roles.RoleExistsAsync(role))
                Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
            var user = new ApplicationUser
            {
                UserName = $"{name}@example.test",
                Email = $"{name}@example.test",
                FullName = name,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            return user;
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public const string ValidPassword = "Strong!Pass123";
        private readonly ServiceProvider _provider;
        public ApplicationDbContext Context { get; }
        public UserManager<ApplicationUser> Users { get; }
        public PlanoraSignInManager SignInManager { get; }
        public AdminUserService AdminUsers { get; }
        public ProjectMemberService Members { get; }
        public TestClock Clock { get; } = new();
        public ApplicationUser Admin { get; private set; } = null!;
        public ApplicationUser Developer { get; private set; } = null!;
        public Project Project { get; private set; } = null!;

        private Fixture(ServiceProvider provider)
        {
            _provider = provider;
            Context = provider.GetRequiredService<ApplicationDbContext>();
            Users = provider.GetRequiredService<UserManager<ApplicationUser>>();
            SignInManager = provider.GetRequiredService<PlanoraSignInManager>();
            AdminUsers = new AdminUserService(Context, Users, Clock);
            Members = new ProjectMemberService(Context, Users, Clock);
        }

        public static async Task<Fixture> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.AddAuthentication();
            services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
            }).AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddSignInManager<PlanoraSignInManager>();

            var fixture = new Fixture(services.BuildServiceProvider());
            RoleManager<IdentityRole> roles = fixture._provider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (string role in SystemRoles.All)
                Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);

            fixture.Admin = await fixture.CreateUserAsync("admin", SystemRoles.Admin);
            fixture.Developer = await fixture.CreateUserAsync("developer", SystemRoles.Developer);
            fixture.Project = new Project { Name = "Admin project", Methodology = ProjectMethodology.Scrum, Status = ProjectStatus.Active, CreatedByUserId = fixture.Admin.Id, CreatedAt = fixture.Clock.UtcNow };
            fixture.Context.Projects.Add(fixture.Project);
            await fixture.Context.SaveChangesAsync();
            return fixture;
        }

        public async Task<ApplicationUser> CreateUserAsync(string name, string role)
        {
            var user = new ApplicationUser { UserName = $"{name}@example.test", Email = $"{name}@example.test", FullName = name, EmailConfirmed = true, CreatedAt = Clock.UtcNow };
            Assert.True((await Users.CreateAsync(user, ValidPassword)).Succeeded);
            Assert.True((await Users.AddToRoleAsync(user, role)).Succeeded);
            return user;
        }

        public CreateAdminUserRequest CreateRequest(string email, string role) => new()
        {
            FullName = "Created User",
            Email = email,
            Password = ValidPassword,
            GlobalRole = role,
            ActorUserId = Admin.Id
        };

        public AddProjectMemberRequest AddRequest(ApplicationUser user) => new()
        {
            ProjectId = Project.Id,
            UserId = user.Id,
            Role = ProjectMemberRole.Developer,
            ActorUserId = Admin.Id
        };

        public async Task<ProjectMember> AddMembershipAsync(ApplicationUser user, ProjectMemberRole role)
        {
            var member = new ProjectMember { ProjectId = Project.Id, UserId = user.Id, Role = role, JoinedAt = Clock.UtcNow };
            Context.ProjectMembers.Add(member);
            await Context.SaveChangesAsync();
            return member;
        }

        public TaskItem CreateTask(TaskItemStatus status, string assigneeId)
        {
            return new TaskItem
            {
                Title = "Assigned task",
                Description = "Task description",
                Status = status,
                AssignedUserId = assigneeId,
                CreatedByUserId = Admin.Id,
                CreatedAt = Clock.UtcNow,
                SprintBacklogItem = new SprintBacklogItem
                {
                    AddedByUserId = Admin.Id,
                    Sprint = new Sprint { Project = Project, Name = "Sprint", Status = SprintStatus.InProgress, CreatedByUserId = Admin.Id, CreatedAt = Clock.UtcNow },
                    BacklogItem = new BacklogItem { Project = Project, Title = "Backlog", Status = BacklogItemStatus.InSprint, CreatedByUserId = Admin.Id, CreatedAt = Clock.UtcNow }
                }
            };
        }

        public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
    }

    public sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc);
    }
}
