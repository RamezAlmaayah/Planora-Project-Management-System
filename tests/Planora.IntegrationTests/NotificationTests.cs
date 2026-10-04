using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Issues;
using Planora.Application.Common.Notifications;
using Planora.Application.Common.Security;
using Planora.Application.Common.Tasks;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Issues;
using Planora.Infrastructure.Notifications;
using Planora.Infrastructure.Persistence;
using Planora.Infrastructure.Scrum;
using Planora.Infrastructure.Tasks;
using Planora.Web.Controllers;
using Planora.Web.ViewModels.Notifications;

namespace Planora.IntegrationTests;

public sealed class NotificationTests
{
    [Fact]
    public async Task TaskAssignmentNotifiesOnlyNewAssignee()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Task.AssignedUserId = null;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Tasks.UpdateAsync(fixture.TaskUpdate(fixture.Developer.Id));

        Assert.True(result.Succeeded);
        Notification notification = Assert.Single(fixture.Context.Notifications);
        Assert.Equal(fixture.Developer.Id, notification.UserId);
        Assert.Equal(NotificationType.TaskAssigned, notification.Type);
        Assert.DoesNotContain(fixture.Context.Notifications,
            item => item.UserId == fixture.OtherDeveloper.Id);
    }

    [Fact]
    public async Task FailedTaskAssignmentCreatesNoNotification()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Task.AssignedUserId = null;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Tasks.UpdateAsync(fixture.TaskUpdate(fixture.ForeignDeveloper.Id));

        Assert.False(result.Succeeded);
        Assert.Empty(fixture.Context.Notifications);
    }

    [Fact]
    public async Task TaskSubmittedToQaNotifiesOnlyEligibleProjectQa()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Task.Status = TaskItemStatus.InProgress;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Tasks.TransitionStatusAsync(
            fixture.TaskTransition(TaskItemStatus.InProgress, TaskItemStatus.InReview));

        Assert.True(result.Succeeded);
        Notification notification = Assert.Single(fixture.Context.Notifications);
        Assert.Equal(fixture.QaTester.Id, notification.UserId);
        Assert.Equal(NotificationType.TaskSentToQa, notification.Type);
        Assert.DoesNotContain(fixture.Context.Notifications,
            item => item.UserId == fixture.ForeignQa.Id);
    }

    [Fact]
    public async Task StaleTaskTransitionCreatesNoDuplicateNotification()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Task.Status = TaskItemStatus.InProgress;
        await fixture.Context.SaveChangesAsync();
        TaskStatusTransitionRequest request = fixture.TaskTransition(
            TaskItemStatus.InProgress, TaskItemStatus.InReview);

        var first = await fixture.Tasks.TransitionStatusAsync(request);
        var stale = await fixture.Tasks.TransitionStatusAsync(request);

        Assert.True(first.Succeeded);
        Assert.False(stale.Succeeded);
        Assert.Single(fixture.Context.Notifications);
    }

    [Fact]
    public async Task QaFailNotifiesAssignedDeveloperWithoutFailureNote()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Task.Status = TaskItemStatus.InReview;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Tasks.ReviewAsync(
            fixture.QaReview(QaReviewResult.Fail, "Sensitive failure details"));

        Assert.True(result.Succeeded);
        Notification notification = Assert.Single(fixture.Context.Notifications);
        Assert.Equal(fixture.Developer.Id, notification.UserId);
        Assert.Equal(NotificationType.QaFailed, notification.Type);
        Assert.DoesNotContain("Sensitive", notification.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QaPassNotifiesDeveloperAndProjectManagers()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Task.Status = TaskItemStatus.InReview;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Tasks.ReviewAsync(fixture.QaReview(QaReviewResult.Pass));

        Assert.True(result.Succeeded);
        Assert.Equal(3, fixture.Context.Notifications.Count());
        Assert.Contains(fixture.Context.Notifications, item => item.UserId == fixture.Developer.Id);
        Assert.Contains(fixture.Context.Notifications, item => item.UserId == fixture.ProjectManager.Id);
        Assert.Contains(fixture.Context.Notifications, item => item.UserId == fixture.ScrumMaster.Id);
        Assert.All(fixture.Context.Notifications,
            item => Assert.Equal(NotificationType.QaPassed, item.Type));
    }

    [Fact]
    public async Task DuplicateQaRequestCreatesNoDuplicateNotification()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Task.Status = TaskItemStatus.InReview;
        await fixture.Context.SaveChangesAsync();
        QaReviewTaskRequest request = fixture.QaReview(QaReviewResult.Fail, "Needs changes");

        var first = await fixture.Tasks.ReviewAsync(request);
        var stale = await fixture.Tasks.ReviewAsync(request);

        Assert.True(first.Succeeded);
        Assert.False(stale.Succeeded);
        Assert.Single(fixture.Context.Notifications);
    }

    [Fact]
    public async Task IssueAssignmentNotifiesAssignedDeveloper()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.AddIssueAsync(IssueStatus.Open, null);

        var result = await fixture.Issues.AssignAsync(new AssignIssueRequest
        {
            ProjectId = fixture.Project.Id,
            IssueId = issue.Id,
            ExpectedStatus = IssueStatus.Open,
            AssignedUserId = fixture.Developer.Id,
            ActorUserId = fixture.ProjectManager.Id
        });

        Assert.True(result.Succeeded);
        Notification notification = Assert.Single(fixture.Context.Notifications);
        Assert.Equal(fixture.Developer.Id, notification.UserId);
        Assert.Equal(NotificationType.IssueAssigned, notification.Type);
    }

    [Fact]
    public async Task IssueResolutionNotifiesEligibleQaOnly()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.AddIssueAsync(IssueStatus.InProgress, fixture.Developer.Id);

        var result = await fixture.Issues.TransitionAsync(fixture.IssueTransition(
            issue, fixture.Developer.Id, IssueStatus.Resolved, "Fixed"));

        Assert.True(result.Succeeded);
        Notification notification = Assert.Single(fixture.Context.Notifications);
        Assert.Equal(fixture.QaTester.Id, notification.UserId);
        Assert.Equal(NotificationType.IssueResolved, notification.Type);
        Assert.DoesNotContain(fixture.Context.Notifications,
            item => item.UserId == fixture.ForeignQa.Id);
    }

    [Theory]
    [InlineData(IssueStatus.Resolved)]
    [InlineData(IssueStatus.Closed)]
    public async Task IssueReopenNotifiesAssignedDeveloper(IssueStatus source)
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.AddIssueAsync(source, fixture.Developer.Id, "Fixed");

        var result = await fixture.Issues.TransitionAsync(fixture.IssueTransition(
            issue, fixture.QaTester.Id, IssueStatus.Reopened));

        Assert.True(result.Succeeded);
        Notification notification = Assert.Single(fixture.Context.Notifications);
        Assert.Equal(fixture.Developer.Id, notification.UserId);
        Assert.Equal(NotificationType.IssueReopened, notification.Type);
    }

    [Fact]
    public async Task IssueCloseNotifiesReporterAndAssignedDeveloperOnly()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.AddIssueAsync(
            IssueStatus.Resolved, fixture.Developer.Id, "Fixed");

        var result = await fixture.Issues.TransitionAsync(fixture.IssueTransition(
            issue, fixture.QaTester.Id, IssueStatus.Closed));

        Assert.True(result.Succeeded);
        Assert.Equal(2, fixture.Context.Notifications.Count());
        Assert.Contains(fixture.Context.Notifications,
            item => item.UserId == fixture.ProjectManager.Id);
        Assert.Contains(fixture.Context.Notifications,
            item => item.UserId == fixture.Developer.Id);
        Assert.DoesNotContain(fixture.Context.Notifications,
            item => item.UserId == fixture.OtherDeveloper.Id);
    }

    [Fact]
    public async Task StaleIssueTransitionCreatesNoNotification()
    {
        await using var fixture = await Fixture.CreateAsync();
        Issue issue = await fixture.AddIssueAsync(
            IssueStatus.Resolved, fixture.Developer.Id, "Fixed");
        TransitionIssueRequest request = fixture.IssueTransition(
            issue, fixture.QaTester.Id, IssueStatus.Closed);

        var first = await fixture.Issues.TransitionAsync(request);
        var stale = await fixture.Issues.TransitionAsync(request);

        Assert.True(first.Succeeded);
        Assert.False(stale.Succeeded);
        Assert.Equal(2, fixture.Context.Notifications.Count());
    }

    [Fact]
    public async Task SprintStartAndCompletionNotifyProjectMembersExceptActor()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Sprint.Status = SprintStatus.NotStarted;
        await fixture.Context.SaveChangesAsync();

        var started = await fixture.Sprints.StartAsync(
            fixture.Project.Id, fixture.Sprint.Id, fixture.ProjectManager.Id);
        Assert.True(started.Succeeded);
        Assert.All(fixture.Context.Notifications,
            item => Assert.Equal(NotificationType.SprintStarted, item.Type));
        Assert.DoesNotContain(fixture.Context.Notifications,
            item => item.UserId == fixture.ProjectManager.Id || item.UserId == fixture.ForeignDeveloper.Id);

        fixture.Context.Notifications.RemoveRange(fixture.Context.Notifications);
        fixture.Task.Status = TaskItemStatus.Done;
        await fixture.Context.SaveChangesAsync();
        var completed = await fixture.Sprints.CompleteAsync(
            fixture.Project.Id, fixture.Sprint.Id, fixture.ProjectManager.Id);

        Assert.True(completed.Succeeded);
        Assert.NotEmpty(fixture.Context.Notifications);
        Assert.All(fixture.Context.Notifications,
            item => Assert.Equal(NotificationType.SprintCompleted, item.Type));
    }

    [Fact]
    public async Task UserQueriesSeeOnlyOwnNotificationsAndUnreadCount()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddNotificationAsync(fixture.Developer.Id, "Mine", false);
        await fixture.AddNotificationAsync(fixture.Developer.Id, "Read", true);
        await fixture.AddNotificationAsync(fixture.OtherDeveloper.Id, "Foreign", false);

        NotificationPage page = await fixture.Notifications.GetPageAsync(
            fixture.Developer.Id, 1, 20);
        int unread = await fixture.Notifications.GetUnreadCountAsync(fixture.Developer.Id);

        Assert.Equal(2, page.TotalCount);
        Assert.All(page.Items,
            item => Assert.DoesNotContain("Foreign", item.Title, StringComparison.Ordinal));
        Assert.Equal(1, unread);
    }

    [Fact]
    public async Task UserCanMarkOnlyOwnNotificationRead()
    {
        await using var fixture = await Fixture.CreateAsync();
        Notification mine = await fixture.AddNotificationAsync(fixture.Developer.Id, "Mine", false);
        Notification other = await fixture.AddNotificationAsync(
            fixture.OtherDeveloper.Id, "Other", false);

        var ownResult = await fixture.Notifications.MarkReadAsync(
            fixture.Developer.Id, mine.Id);
        var foreignResult = await fixture.Notifications.MarkReadAsync(
            fixture.Developer.Id, other.Id);

        Assert.True(ownResult.Succeeded);
        Assert.False(foreignResult.Succeeded);
        Assert.True(mine.IsRead);
        Assert.False(other.IsRead);
    }

    [Fact]
    public async Task MarkAllAffectsOnlyCurrentUser()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddNotificationAsync(fixture.Developer.Id, "First", false);
        await fixture.AddNotificationAsync(fixture.Developer.Id, "Second", false);
        Notification other = await fixture.AddNotificationAsync(
            fixture.OtherDeveloper.Id, "Other", false);

        int changed = await fixture.Notifications.MarkAllReadAsync(fixture.Developer.Id);

        Assert.Equal(2, changed);
        Assert.All(fixture.Context.Notifications.Where(item => item.UserId == fixture.Developer.Id),
            item => Assert.True(item.IsRead));
        Assert.False(other.IsRead);
    }

    [Fact]
    public async Task ForgedNotificationIdIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Notifications.MarkReadAsync(
            fixture.Developer.Id, int.MaxValue);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task PaginationAndRecentOrderingAreBoundedAndNewestFirst()
    {
        await using var fixture = await Fixture.CreateAsync();
        for (int index = 0; index < 25; index++)
        {
            fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(1);
            await fixture.AddNotificationAsync(
                fixture.Developer.Id, $"Notification {index:D2}", false);
        }

        NotificationPage page = await fixture.Notifications.GetPageAsync(
            fixture.Developer.Id, 2, 20);
        IReadOnlyList<NotificationSummary> recent = await fixture.Notifications.GetRecentAsync(
            fixture.Developer.Id, 7);

        Assert.Equal(25, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(5, page.Items.Count);
        Assert.Equal(7, recent.Count);
        Assert.Equal("Notification 24", recent[0].Title);
        Assert.True(recent.Zip(recent.Skip(1),
            (newer, older) => newer.CreatedAt >= older.CreatedAt).All(value => value));
    }

    [Fact]
    public async Task CrossProjectRecipientAndUnsafeUrlAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();

        bool wrongProject = await fixture.Notifications.EnqueueAsync(
            fixture.NotificationRequest(
                fixture.Developer.Id, "Wrong project", "/Projects/Details?id=2",
                fixture.ForeignProject.Id));
        bool unsafeUrl = await fixture.Notifications.EnqueueAsync(
            fixture.NotificationRequest(
                fixture.Developer.Id, "Unsafe", "https://evil.example/path"));

        Assert.False(wrongProject);
        Assert.False(unsafeUrl);
        Assert.Empty(fixture.Context.Notifications);
    }

    [Fact]
    public void NotificationUiIsAuthorizedEncodedAndMutationsUseAntiforgery()
    {
        Assert.NotNull(typeof(NotificationsController).GetCustomAttribute<AuthorizeAttribute>());
        MethodInfo[] posts = typeof(NotificationsController).GetMethods()
            .Where(method => method.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToArray();
        Assert.Equal(["MarkAllRead", "MarkRead"],
            posts.Select(method => method.Name).OrderBy(name => name).ToArray());
        Assert.All(posts, method => Assert.NotNull(
            method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));

        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", ".."));
        string view = File.ReadAllText(Path.Combine(
            root, "src", "Planora.Web", "Views", "Notifications", "Index.cshtml"));
        Assert.DoesNotContain("Html.Raw", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@notification.Message", view, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnonymousNotificationPageIsChallenged()
    {
        await using var fixture = await Fixture.CreateAsync();
        var controller = new NotificationsController(fixture.Notifications)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        IActionResult result = await controller.Index();

        Assert.IsType<ChallengeResult>(result);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        public ApplicationDbContext Context { get; }
        public MutableClock Clock { get; } = new();
        public NotificationService Notifications { get; }
        public TaskService Tasks { get; }
        public IssueService Issues { get; }
        public SprintService Sprints { get; }
        public Project Project { get; }
        public Project ForeignProject { get; }
        public Sprint Sprint { get; }
        public SprintBacklogItem Link { get; }
        public TaskItem Task { get; }
        public ApplicationUser ProjectManager { get; }
        public ApplicationUser ScrumMaster { get; }
        public ApplicationUser Developer { get; }
        public ApplicationUser OtherDeveloper { get; }
        public ApplicationUser QaTester { get; }
        public ApplicationUser ForeignDeveloper { get; }
        public ApplicationUser ForeignQa { get; }

        private Fixture(ServiceProvider provider)
        {
            _provider = provider;
            Context = provider.GetRequiredService<ApplicationDbContext>();
            UserManager<ApplicationUser> users = provider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager<IdentityRole> roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (string role in SystemRoles.All)
                Assert.True(roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult().Succeeded);

            ProjectManager = CreateUser(users, "manager", SystemRoles.ProjectManager);
            ScrumMaster = CreateUser(users, "scrum", SystemRoles.ScrumMaster);
            Developer = CreateUser(users, "developer", SystemRoles.Developer);
            OtherDeveloper = CreateUser(users, "other-developer", SystemRoles.Developer);
            QaTester = CreateUser(users, "qa", SystemRoles.QaTester);
            ForeignDeveloper = CreateUser(users, "foreign-developer", SystemRoles.Developer);
            ForeignQa = CreateUser(users, "foreign-qa", SystemRoles.QaTester);

            Project = new Project
            {
                Name = "Planora",
                Methodology = ProjectMethodology.Scrum,
                Status = ProjectStatus.Active
            };
            ForeignProject = new Project
            {
                Name = "Foreign",
                Methodology = ProjectMethodology.Scrum,
                Status = ProjectStatus.Active
            };
            Sprint = new Sprint
            {
                Project = Project,
                Name = "Sprint 1",
                Goal = "Ship",
                StartDate = Clock.UtcNow.Date,
                EndDate = Clock.UtcNow.Date.AddDays(14),
                Status = SprintStatus.InProgress,
                CreatedByUserId = ProjectManager.Id,
                CreatedAt = Clock.UtcNow
            };
            Link = new SprintBacklogItem
            {
                Sprint = Sprint,
                BacklogItem = new BacklogItem
                {
                    Project = Project,
                    Title = "Story",
                    Description = "Story",
                    Status = BacklogItemStatus.InSprint,
                    CreatedByUserId = ProjectManager.Id,
                    CreatedAt = Clock.UtcNow
                },
                AddedByUserId = ProjectManager.Id,
                AddedAt = Clock.UtcNow
            };
            Task = new TaskItem
            {
                SprintBacklogItem = Link,
                Title = "Secure notifications",
                Description = "Description",
                Status = TaskItemStatus.ToDo,
                AssignedUserId = Developer.Id,
                CreatedByUserId = ProjectManager.Id,
                CreatedAt = Clock.UtcNow
            };
            Context.AddRange(
                Task,
                ForeignProject,
                new ProjectMember { Project = Project, UserId = ProjectManager.Id, Role = ProjectMemberRole.ProjectManager },
                new ProjectMember { Project = Project, UserId = ScrumMaster.Id, Role = ProjectMemberRole.ScrumMaster },
                new ProjectMember { Project = Project, UserId = Developer.Id, Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = Project, UserId = OtherDeveloper.Id, Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = Project, UserId = QaTester.Id, Role = ProjectMemberRole.QaTester },
                new ProjectMember { Project = ForeignProject, UserId = ForeignDeveloper.Id, Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = ForeignProject, UserId = ForeignQa.Id, Role = ProjectMemberRole.QaTester });
            Context.SaveChanges();

            Notifications = new NotificationService(Context, Clock);
            Tasks = new TaskService(
                Context, new TaskAssignmentService(Context), Clock, Notifications);
            Issues = new IssueService(Context, Clock, Notifications);
            Sprints = new SprintService(Context, Clock, Notifications);
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

        public UpdateTaskRequest TaskUpdate(string assigneeId) => new()
        {
            Id = Task.Id,
            ProjectId = Project.Id,
            SprintId = Sprint.Id,
            SprintBacklogItemId = Link.Id,
            Title = Task.Title,
            Description = Task.Description,
            Priority = Task.Priority,
            AssignedUserId = assigneeId,
            Deadline = Task.Deadline,
            UpdatedByUserId = ProjectManager.Id
        };

        public TaskStatusTransitionRequest TaskTransition(
            TaskItemStatus source,
            TaskItemStatus target) => new()
        {
            ProjectId = Project.Id,
            SprintId = Sprint.Id,
            TaskId = Task.Id,
            CurrentStatus = source,
            TargetStatus = target,
            ActorUserId = Developer.Id,
            Actor = TaskTransitionActor.Developer
        };

        public QaReviewTaskRequest QaReview(QaReviewResult result, string? notes = null) => new()
        {
            ProjectId = Project.Id,
            SprintId = Sprint.Id,
            TaskId = Task.Id,
            ReviewerUserId = QaTester.Id,
            Result = result,
            Notes = notes
        };

        public async Task<Issue> AddIssueAsync(
            IssueStatus status,
            string? assigneeId,
            string? resolutionNotes = null)
        {
            var issue = new Issue
            {
                ProjectId = Project.Id,
                TaskItemId = Task.Id,
                Title = "Notification bug",
                Description = "Description",
                Status = status,
                Severity = IssueSeverity.High,
                Priority = PriorityLevel.High,
                ReporterUserId = ProjectManager.Id,
                AssignedUserId = assigneeId,
                ResolutionNotes = resolutionNotes,
                CreatedAt = Clock.UtcNow
            };
            Context.Add(issue);
            await Context.SaveChangesAsync();
            return issue;
        }

        public TransitionIssueRequest IssueTransition(
            Issue issue,
            string actorUserId,
            IssueStatus target,
            string? notes = null) => new()
        {
            ProjectId = Project.Id,
            IssueId = issue.Id,
            ExpectedStatus = issue.Status,
            TargetStatus = target,
            ResolutionNotes = notes,
            ActorUserId = actorUserId
        };

        public CreateNotificationRequest NotificationRequest(
            string recipient,
            string title,
            string? targetUrl = "/Projects/Details?id=1",
            int? projectId = null) => new()
        {
            RecipientUserId = recipient,
            ProjectId = projectId ?? Project.Id,
            Type = NotificationType.TaskAssigned,
            Title = title,
            Message = "Message",
            TargetUrl = targetUrl
        };

        public async Task<Notification> AddNotificationAsync(
            string recipient,
            string title,
            bool isRead)
        {
            var notification = new Notification
            {
                UserId = recipient,
                ProjectId = Project.Id,
                Type = NotificationType.TaskAssigned,
                Title = title,
                Message = "Message",
                TargetUrl = "/Projects/Details?id=1",
                IsRead = isRead,
                CreatedAt = Clock.UtcNow,
                ReadAt = isRead ? Clock.UtcNow : null
            };
            Context.Add(notification);
            await Context.SaveChangesAsync();
            return notification;
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

    private sealed class MutableClock : IClock
    {
        public DateTime UtcNow { get; set; } =
            new(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc);
    }
}
