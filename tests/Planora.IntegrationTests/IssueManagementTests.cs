using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Issues;
using Planora.Application.Common.Security;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Issues;
using Planora.Infrastructure.Persistence;
using Planora.Infrastructure.Projects;
using Planora.Infrastructure.Security;
using Planora.Web.Controllers;
using Planora.Web.ViewModels.Issues;

namespace Planora.IntegrationTests;

public sealed class IssueManagementTests
{
    [Fact]
    public async Task AuthorizedMemberCanViewProjectIssues()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.SeedIssueAsync();
        var controller = fixture.CreateController(fixture.Developer);

        var result = Assert.IsType<ViewResult>(await controller.Index(
            fixture.Project.Id, null, null, null, null, null, default));
        var model = Assert.IsType<IssueIndexViewModel>(result.Model);

        Assert.Single(model.Issues);
    }

    [Fact]
    public async Task NonMemberCannotViewProjectIssues()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = fixture.CreateController(fixture.Outsider);

        var result = await controller.Index(
            fixture.Project.Id, null, null, null, null, null, default);

        Assert.IsType<ForbidResult>(result);
    }

    [Fact]
    public async Task ProjectMemberCanCreateOpenIssue()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.Developer.Id));
        var issue = await fixture.Context.Issues.SingleAsync();

        Assert.True(result.Succeeded);
        Assert.Equal(IssueStatus.Open, issue.Status);
        Assert.Equal(fixture.Developer.Id, issue.ReporterUserId);
        Assert.Contains(fixture.Context.ActivityLogs,
            log => log.Action == "IssueCreated");
    }

    [Fact]
    public async Task NonMemberCannotCreateIssue()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.Outsider.Id));

        Assert.Equal(IssueOperationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task ArchivedProjectRejectsIssueCreation()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Project.Status = ProjectStatus.Archived;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.Developer.Id));

        Assert.Equal(IssueOperationFailure.ReadOnly, result.Failure);
    }

    [Fact]
    public async Task CrossProjectTaskIsRejectedDuringIssueCreation()
    {
        await using var fixture = await Fixture.CreateAsync();
        CreateIssueRequest source = fixture.CreateRequest(fixture.Developer.Id);

        var result = await fixture.Service.CreateAsync(new CreateIssueRequest
        {
            ProjectId = source.ProjectId,
            TaskItemId = fixture.ForeignTask.Id,
            Title = source.Title,
            Description = source.Description,
            Severity = source.Severity,
            Priority = source.Priority,
            ReporterUserId = source.ReporterUserId
        });

        Assert.Equal(IssueOperationFailure.Validation, result.Failure);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidIssueTitleIsRejected(string title)
    {
        await using var fixture = await Fixture.CreateAsync();
        CreateIssueRequest source = fixture.CreateRequest(fixture.Developer.Id);

        var result = await fixture.Service.CreateAsync(new CreateIssueRequest
        {
            ProjectId = source.ProjectId,
            Title = title,
            Description = source.Description,
            Severity = source.Severity,
            Priority = source.Priority,
            ReporterUserId = source.ReporterUserId
        });

        Assert.Equal(IssueOperationFailure.Validation, result.Failure);
    }

    [Fact]
    public async Task ForgedCreateEnumIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        CreateIssueRequest source = fixture.CreateRequest(fixture.Developer.Id);

        var result = await fixture.Service.CreateAsync(new CreateIssueRequest
        {
            ProjectId = source.ProjectId,
            Title = source.Title,
            Description = source.Description,
            Severity = (IssueSeverity)999,
            Priority = source.Priority,
            ReporterUserId = source.ReporterUserId
        });

        Assert.Equal(IssueOperationFailure.Validation, result.Failure);
    }

    [Fact]
    public async Task IssueHtmlIsRenderedThroughRazorEncoding()
    {
        await using var fixture = await Fixture.CreateAsync();
        const string payload = "<script>alert('xss')</script>";
        CreateIssueRequest source = fixture.CreateRequest(fixture.Developer.Id);
        var request = new CreateIssueRequest
        {
            ProjectId = source.ProjectId,
            Title = payload,
            Description = payload,
            Severity = source.Severity,
            Priority = source.Priority,
            ReporterUserId = source.ReporterUserId
        };

        var result = await fixture.Service.CreateAsync(request);
        string view = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "Planora.Web", "Views", "Issues", "Details.cshtml"));

        Assert.True(result.Succeeded);
        Assert.Equal(payload, (await fixture.Context.Issues.SingleAsync()).Description);
        Assert.DoesNotContain("Html.Raw", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p>@line</p>", view, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("manager")]
    [InlineData("scrum")]
    public async Task AuthorizedManagersCanAssignIssue(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync();
        string actorId = fixture.Actor(actor).Id;

        var result = await fixture.Service.AssignAsync(
            fixture.AssignRequest(issue, actorId, fixture.Developer.Id));

        Assert.True(result.Succeeded);
        Assert.Equal(IssueStatus.Assigned, issue.Status);
        Assert.Equal(fixture.Developer.Id, issue.AssignedUserId);
        Assert.Contains(fixture.Context.ActivityLogs,
            log => log.Action == "IssueAssigned");
    }

    [Theory]
    [InlineData("developer")]
    [InlineData("qa")]
    public async Task DeveloperAndQaCannotAssignIssue(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync();

        var result = await fixture.Service.AssignAsync(
            fixture.AssignRequest(issue, fixture.Actor(actor).Id, fixture.OtherDeveloper.Id));

        Assert.Equal(IssueOperationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task AssigneeMustBeSameProjectDeveloper()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync();

        var foreign = await fixture.Service.AssignAsync(
            fixture.AssignRequest(issue, fixture.ProjectManager.Id, fixture.ForeignDeveloper.Id));
        var nonDeveloper = await fixture.Service.AssignAsync(
            fixture.AssignRequest(issue, fixture.ProjectManager.Id, fixture.QaTester.Id));

        Assert.Equal(IssueOperationFailure.Validation, foreign.Failure);
        Assert.Equal(IssueOperationFailure.Validation, nonDeveloper.Failure);
    }

    [Theory]
    [InlineData(IssueStatus.InProgress)]
    [InlineData(IssueStatus.Resolved)]
    [InlineData(IssueStatus.Closed)]
    public async Task ReassignmentIsForbiddenAfterWorkStarts(IssueStatus status)
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(status, fixture.Developer.Id);

        var result = await fixture.Service.AssignAsync(
            fixture.AssignRequest(issue, fixture.ProjectManager.Id, fixture.OtherDeveloper.Id));

        Assert.Equal(IssueOperationFailure.Conflict, result.Failure);
    }

    [Fact]
    public async Task ReopenedIssueCanBeReassignedWithoutChangingStatus()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            IssueStatus.Reopened, fixture.Developer.Id);

        var result = await fixture.Service.AssignAsync(
            fixture.AssignRequest(issue, fixture.ScrumMaster.Id, fixture.OtherDeveloper.Id));

        Assert.True(result.Succeeded);
        Assert.Equal(IssueStatus.Reopened, issue.Status);
        Assert.Equal(fixture.OtherDeveloper.Id, issue.AssignedUserId);
    }

    [Fact]
    public async Task DuplicateAssignmentIsRejectedAsStale()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync();
        AssignIssueRequest request = fixture.AssignRequest(
            issue, fixture.ProjectManager.Id, fixture.Developer.Id);

        var first = await fixture.Service.AssignAsync(request);
        var second = await fixture.Service.AssignAsync(request);

        Assert.True(first.Succeeded);
        Assert.Equal(IssueOperationFailure.Conflict, second.Failure);
    }

    [Fact]
    public async Task AssignedDeveloperCanStartAssignedIssue()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            IssueStatus.Assigned, fixture.Developer.Id);

        var result = await fixture.Service.TransitionAsync(
            fixture.Transition(issue, fixture.Developer.Id, IssueStatus.InProgress));

        Assert.True(result.Succeeded);
        Assert.Equal(IssueStatus.InProgress, issue.Status);
    }

    [Fact]
    public async Task AnotherDeveloperCannotStartAssignedIssue()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            IssueStatus.Assigned, fixture.Developer.Id);

        var result = await fixture.Service.TransitionAsync(
            fixture.Transition(issue, fixture.OtherDeveloper.Id, IssueStatus.InProgress));

        Assert.Equal(IssueOperationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task AssignedDeveloperCanStartReopenedIssue()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            IssueStatus.Reopened, fixture.Developer.Id);

        var result = await fixture.Service.TransitionAsync(
            fixture.Transition(issue, fixture.Developer.Id, IssueStatus.InProgress));

        Assert.True(result.Succeeded);
        Assert.Equal(IssueStatus.InProgress, issue.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveRequiresResolutionNotes(string? notes)
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            IssueStatus.InProgress, fixture.Developer.Id);

        var result = await fixture.Service.TransitionAsync(
            fixture.Transition(issue, fixture.Developer.Id, IssueStatus.Resolved, notes));

        Assert.Equal(IssueOperationFailure.Validation, result.Failure);
        Assert.Equal(IssueStatus.InProgress, issue.Status);
    }

    [Fact]
    public async Task AssignedDeveloperCanResolveWithNotes()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            IssueStatus.InProgress, fixture.Developer.Id);

        var result = await fixture.Service.TransitionAsync(
            fixture.Transition(
                issue, fixture.Developer.Id, IssueStatus.Resolved, "Fixed and verified locally."));

        Assert.True(result.Succeeded);
        Assert.Equal(IssueStatus.Resolved, issue.Status);
        Assert.Equal("Fixed and verified locally.", issue.ResolutionNotes);
        Assert.Contains(fixture.Context.ActivityLogs,
            log => log.Action == "IssueResolved");
    }

    [Theory]
    [InlineData(IssueStatus.Resolved, IssueStatus.Closed)]
    [InlineData(IssueStatus.Resolved, IssueStatus.Reopened)]
    [InlineData(IssueStatus.Closed, IssueStatus.Reopened)]
    public async Task MatchingQaCanPerformVerificationTransitions(
        IssueStatus source,
        IssueStatus target)
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            source, fixture.Developer.Id, "Resolution details");

        var result = await fixture.Service.TransitionAsync(
            fixture.Transition(issue, fixture.QaTester.Id, target));

        Assert.True(result.Succeeded);
        Assert.Equal(target, issue.Status);
        string action = target == IssueStatus.Closed ? "IssueClosed" : "IssueReopened";
        Assert.Contains(fixture.Context.ActivityLogs, log => log.Action == action);
    }

    [Theory]
    [InlineData("qa-outsider")]
    [InlineData("qa-wrong-role")]
    [InlineData("admin")]
    [InlineData("manager")]
    [InlineData("scrum")]
    [InlineData("developer")]
    public async Task UnauthorizedActorsCannotCloseResolvedIssue(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            IssueStatus.Resolved, fixture.Developer.Id, "Resolution details");

        var result = await fixture.Service.TransitionAsync(
            fixture.Transition(issue, fixture.Actor(actor).Id, IssueStatus.Closed));

        Assert.Equal(IssueOperationFailure.Forbidden, result.Failure);
    }

    [Theory]
    [InlineData(IssueStatus.Open, IssueStatus.InProgress)]
    [InlineData(IssueStatus.Open, IssueStatus.Resolved)]
    [InlineData(IssueStatus.Assigned, IssueStatus.Resolved)]
    [InlineData(IssueStatus.InProgress, IssueStatus.Closed)]
    [InlineData(IssueStatus.Resolved, IssueStatus.InProgress)]
    [InlineData(IssueStatus.Closed, IssueStatus.Resolved)]
    [InlineData(IssueStatus.Reopened, IssueStatus.Closed)]
    public async Task UndefinedTransitionsAreRejected(
        IssueStatus source,
        IssueStatus target)
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            source, fixture.Developer.Id, "Resolution details");

        var result = await fixture.Service.TransitionAsync(
            fixture.Transition(issue, fixture.Admin.Id, target, "Notes"));

        Assert.Equal(IssueOperationFailure.Validation, result.Failure);
    }

    [Fact]
    public async Task ForgedTransitionEnumIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            IssueStatus.Assigned, fixture.Developer.Id);

        var result = await fixture.Service.TransitionAsync(
            fixture.Transition(issue, fixture.Developer.Id, (IssueStatus)999));

        Assert.Equal(IssueOperationFailure.Validation, result.Failure);
    }

    [Fact]
    public async Task DuplicateStatusTransitionIsRejectedAsStale()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            IssueStatus.Assigned, fixture.Developer.Id);
        TransitionIssueRequest request = fixture.Transition(
            issue, fixture.Developer.Id, IssueStatus.InProgress);

        var first = await fixture.Service.TransitionAsync(request);
        var second = await fixture.Service.TransitionAsync(request);

        Assert.True(first.Succeeded);
        Assert.Equal(IssueOperationFailure.Conflict, second.Failure);
    }

    [Fact]
    public async Task ReporterCanEditOwnOpenIssue()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            reporterId: fixture.Developer.Id);

        var result = await fixture.Service.UpdateAsync(
            fixture.UpdateRequest(issue, fixture.Developer.Id, "Updated title"));

        Assert.True(result.Succeeded);
        Assert.Equal("Updated title", issue.Title);
        Assert.Contains(fixture.Context.ActivityLogs,
            log => log.Action == "IssueEdited");
    }

    [Fact]
    public async Task ReporterCannotEditAssignedIssue()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            IssueStatus.Assigned, fixture.OtherDeveloper.Id,
            reporterId: fixture.Developer.Id);

        var result = await fixture.Service.UpdateAsync(
            fixture.UpdateRequest(issue, fixture.Developer.Id, "Blocked"));

        Assert.Equal(IssueOperationFailure.Forbidden, result.Failure);
    }

    [Theory]
    [InlineData("admin", IssueStatus.Open)]
    [InlineData("admin", IssueStatus.Assigned)]
    [InlineData("manager", IssueStatus.Open)]
    [InlineData("manager", IssueStatus.Assigned)]
    [InlineData("scrum", IssueStatus.Open)]
    [InlineData("scrum", IssueStatus.Assigned)]
    public async Task ManagersCanEditOpenAndAssignedIssues(
        string actor,
        IssueStatus status)
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            status,
            status == IssueStatus.Assigned ? fixture.Developer.Id : null);

        var result = await fixture.Service.UpdateAsync(
            fixture.UpdateRequest(issue, fixture.Actor(actor).Id, "Managed title"));

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("developer")]
    [InlineData("qa")]
    public async Task NonReporterDeveloperAndQaCannotEditIssue(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(reporterId: fixture.ProjectManager.Id);

        var result = await fixture.Service.UpdateAsync(
            fixture.UpdateRequest(issue, fixture.Actor(actor).Id, "Blocked"));

        Assert.Equal(IssueOperationFailure.Forbidden, result.Failure);
    }

    [Theory]
    [InlineData(IssueStatus.InProgress)]
    [InlineData(IssueStatus.Resolved)]
    [InlineData(IssueStatus.Closed)]
    [InlineData(IssueStatus.Reopened)]
    public async Task IssueContentIsReadOnlyAfterWorkStarts(IssueStatus status)
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            status, fixture.Developer.Id, "Resolution details");

        var result = await fixture.Service.UpdateAsync(
            fixture.UpdateRequest(issue, fixture.Admin.Id, "Blocked"));

        Assert.Equal(IssueOperationFailure.ReadOnly, result.Failure);
    }

    [Fact]
    public async Task CrossProjectIssueIdAndTaskIdAreRejectedOnEdit()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue foreignIssue = await fixture.SeedForeignIssueAsync();
        Issue localIssue = await fixture.SeedIssueAsync();
        UpdateIssueRequest wrongIssue = fixture.UpdateRequest(
            foreignIssue, fixture.Admin.Id, "Blocked", fixture.Project.Id);
        UpdateIssueRequest wrongTask = fixture.UpdateRequest(
            localIssue, fixture.ProjectManager.Id, "Blocked",
            taskItemId: fixture.ForeignTask.Id);

        var first = await fixture.Service.UpdateAsync(wrongIssue);
        var second = await fixture.Service.UpdateAsync(wrongTask);

        Assert.Equal(IssueOperationFailure.NotFound, first.Failure);
        Assert.Equal(IssueOperationFailure.Validation, second.Failure);
    }

    [Fact]
    public async Task ArchivedProjectRejectsAllIssueMutations()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.SeedIssueAsync(
            IssueStatus.Assigned, fixture.Developer.Id);
        fixture.Project.Status = ProjectStatus.Archived;
        await fixture.Context.SaveChangesAsync();

        var edit = await fixture.Service.UpdateAsync(
            fixture.UpdateRequest(issue, fixture.ProjectManager.Id, "Blocked"));
        var assign = await fixture.Service.AssignAsync(
            fixture.AssignRequest(issue, fixture.ProjectManager.Id, fixture.OtherDeveloper.Id));
        var transition = await fixture.Service.TransitionAsync(
            fixture.Transition(issue, fixture.Developer.Id, IssueStatus.InProgress));

        Assert.Equal(IssueOperationFailure.ReadOnly, edit.Failure);
        Assert.Equal(IssueOperationFailure.ReadOnly, assign.Failure);
        Assert.Equal(IssueOperationFailure.ReadOnly, transition.Failure);
    }

    [Fact]
    public void IssueMutationEndpointsRequireAntiforgeryAndNoDeleteExists()
    {
        Assert.NotNull(typeof(IssuesController).GetCustomAttribute<AuthorizeAttribute>());
        MethodInfo[] posts = typeof(IssuesController).GetMethods()
            .Where(method => method.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToArray();

        Assert.Equal(
            ["Assign", "Create", "Edit", "Transition"],
            posts.Select(method => method.Name).OrderBy(name => name).ToArray());
        Assert.All(posts, method => Assert.NotNull(
            method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
        Assert.DoesNotContain(typeof(IssuesController).GetMethods(),
            method => method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        Assert.Null(typeof(IssueFormViewModel).GetProperty("Status"));
        Assert.Null(typeof(IssueFormViewModel).GetProperty("AssignedUserId"));
    }

    private static string RepositoryRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        public ApplicationDbContext Context { get; }
        public IssueService Service { get; }
        public TestClock Clock { get; } = new();
        public Project Project { get; }
        public Project ForeignProject { get; }
        public TaskItem Task { get; }
        public TaskItem ForeignTask { get; }
        public ApplicationUser Admin { get; }
        public ApplicationUser ProjectManager { get; }
        public ApplicationUser ScrumMaster { get; }
        public ApplicationUser Developer { get; }
        public ApplicationUser OtherDeveloper { get; }
        public ApplicationUser QaTester { get; }
        public ApplicationUser Outsider { get; }
        public ApplicationUser QaOutsider { get; }
        public ApplicationUser WrongRoleQa { get; }
        public ApplicationUser ForeignDeveloper { get; }

        private Fixture(ServiceProvider provider)
        {
            _provider = provider;
            Context = provider.GetRequiredService<ApplicationDbContext>();
            UserManager<ApplicationUser> users = provider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager<IdentityRole> roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (string role in SystemRoles.All)
                Assert.True(roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult().Succeeded);

            Admin = CreateUser(users, "admin", SystemRoles.Admin);
            ProjectManager = CreateUser(users, "manager", SystemRoles.ProjectManager);
            ScrumMaster = CreateUser(users, "scrum", SystemRoles.ScrumMaster);
            Developer = CreateUser(users, "developer", SystemRoles.Developer);
            OtherDeveloper = CreateUser(users, "developer2", SystemRoles.Developer);
            QaTester = CreateUser(users, "qa", SystemRoles.QaTester);
            Outsider = CreateUser(users, "outsider", SystemRoles.Developer);
            QaOutsider = CreateUser(users, "qa-outsider", SystemRoles.QaTester);
            WrongRoleQa = CreateUser(users, "qa-wrong", SystemRoles.QaTester);
            ForeignDeveloper = CreateUser(users, "foreign-developer", SystemRoles.Developer);

            Project = new Project
            {
                Name = "Project",
                Methodology = ProjectMethodology.Scrum,
                Status = ProjectStatus.Active
            };
            ForeignProject = new Project
            {
                Name = "Foreign",
                Methodology = ProjectMethodology.Scrum,
                Status = ProjectStatus.Active
            };
            Task = CreateTask(Project, "Task", ProjectManager.Id);
            ForeignTask = CreateTask(ForeignProject, "Foreign task", ProjectManager.Id);
            Context.AddRange(
                Task,
                ForeignTask,
                new ProjectMember { Project = Project, UserId = ProjectManager.Id, Role = ProjectMemberRole.ProjectManager },
                new ProjectMember { Project = Project, UserId = ScrumMaster.Id, Role = ProjectMemberRole.ScrumMaster },
                new ProjectMember { Project = Project, UserId = Developer.Id, Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = Project, UserId = OtherDeveloper.Id, Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = Project, UserId = QaTester.Id, Role = ProjectMemberRole.QaTester },
                new ProjectMember { Project = Project, UserId = WrongRoleQa.Id, Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = ForeignProject, UserId = ForeignDeveloper.Id, Role = ProjectMemberRole.Developer });
            Context.SaveChanges();
            Service = new IssueService(Context, Clock);
        }

        public static Task<Fixture> CreateAsync()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            return System.Threading.Tasks.Task.FromResult(
                new Fixture(services.BuildServiceProvider()));
        }

        public CreateIssueRequest CreateRequest(string reporterId) => new()
        {
            ProjectId = Project.Id,
            TaskItemId = Task.Id,
            Title = "Login validation fails",
            Description = "The validation response is incorrect.",
            Severity = IssueSeverity.High,
            Priority = PriorityLevel.High,
            ReporterUserId = reporterId
        };

        public async Task<Issue> SeedIssueAsync(
            IssueStatus status = IssueStatus.Open,
            string? assigneeId = null,
            string? resolutionNotes = null,
            string? reporterId = null)
        {
            var issue = new Issue
            {
                ProjectId = Project.Id,
                TaskItemId = Task.Id,
                Title = "Issue",
                Description = "Description",
                Severity = IssueSeverity.Medium,
                Priority = PriorityLevel.Medium,
                Status = status,
                ReporterUserId = reporterId ?? ProjectManager.Id,
                AssignedUserId = assigneeId,
                ResolutionNotes = resolutionNotes,
                CreatedAt = Clock.UtcNow
            };
            Context.Add(issue);
            await Context.SaveChangesAsync();
            return issue;
        }

        public async Task<Issue> SeedForeignIssueAsync()
        {
            var issue = new Issue
            {
                ProjectId = ForeignProject.Id,
                TaskItemId = ForeignTask.Id,
                Title = "Foreign issue",
                Description = "Description",
                ReporterUserId = ProjectManager.Id,
                Status = IssueStatus.Open,
                CreatedAt = Clock.UtcNow
            };
            Context.Add(issue);
            await Context.SaveChangesAsync();
            return issue;
        }

        public AssignIssueRequest AssignRequest(
            Issue issue,
            string actorId,
            string assignedUserId) => new()
            {
                ProjectId = Project.Id,
                IssueId = issue.Id,
                ExpectedStatus = issue.Status,
                ExpectedAssignedUserId = issue.AssignedUserId,
                AssignedUserId = assignedUserId,
                ActorUserId = actorId
            };

        public TransitionIssueRequest Transition(
            Issue issue,
            string actorId,
            IssueStatus target,
            string? notes = null) => new()
            {
                ProjectId = Project.Id,
                IssueId = issue.Id,
                ExpectedStatus = issue.Status,
                TargetStatus = target,
                ResolutionNotes = notes,
                ActorUserId = actorId
            };

        public UpdateIssueRequest UpdateRequest(
            Issue issue,
            string actorId,
            string title,
            int? projectId = null,
            int? taskItemId = null) => new()
            {
                ProjectId = projectId ?? Project.Id,
                IssueId = issue.Id,
                ExpectedStatus = issue.Status,
                TaskItemId = taskItemId ?? issue.TaskItemId,
                Title = title,
                Description = issue.Description,
                Severity = issue.Severity,
                Priority = issue.Priority,
                ActorUserId = actorId
            };

        public ApplicationUser Actor(string name) => name switch
        {
            "admin" => Admin,
            "manager" => ProjectManager,
            "scrum" => ScrumMaster,
            "qa" => QaTester,
            "qa-outsider" => QaOutsider,
            "qa-wrong-role" => WrongRoleQa,
            _ => Developer
        };

        public IssuesController CreateController(ApplicationUser user)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user.Id)], "Test");
            foreach (string role in Context.UserRoles
                .Where(item => item.UserId == user.Id)
                .Join(Context.Roles, item => item.RoleId, role => role.Id,
                    (_, role) => role.Name!)
                .ToList())
                identity.AddClaim(new Claim(ClaimTypes.Role, role));

            return new IssuesController(
                Service,
                new ProjectService(Context, Clock),
                new ProjectAccessService(Context),
                NullLogger<IssuesController>.Instance)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(identity)
                    }
                }
            };
        }

        private static TaskItem CreateTask(Project project, string title, string creatorId)
        {
            return new TaskItem
            {
                Title = title,
                CreatedByUserId = creatorId,
                SprintBacklogItem = new SprintBacklogItem
                {
                    Sprint = new Sprint
                    {
                        Project = project,
                        Name = $"{title} sprint",
                        Status = SprintStatus.InProgress
                    },
                    BacklogItem = new BacklogItem
                    {
                        Project = project,
                        Title = $"{title} backlog",
                        Status = BacklogItemStatus.InSprint
                    }
                }
            };
        }

        private static ApplicationUser CreateUser(
            UserManager<ApplicationUser> users,
            string name,
            string role)
        {
            var user = new ApplicationUser
            {
                UserName = $"{name}@example.test",
                Email = $"{name}@example.test",
                FullName = name,
                EmailConfirmed = true
            };
            Assert.True(users.CreateAsync(user).GetAwaiter().GetResult().Succeeded);
            Assert.True(users.AddToRoleAsync(user, role).GetAwaiter().GetResult().Succeeded);
            return user;
        }

        public async ValueTask DisposeAsync() => await _provider.DisposeAsync();
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
    }
}
