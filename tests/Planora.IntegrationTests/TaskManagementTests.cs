using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Security;
using Planora.Application.Common.Tasks;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;
using Planora.Infrastructure.Projects;
using Planora.Infrastructure.Scrum;
using Planora.Infrastructure.Security;
using Planora.Infrastructure.Tasks;
using Planora.Web.Controllers;
using Planora.Web.ViewModels.Tasks;

namespace Planora.IntegrationTests;

public sealed class TaskManagementTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("developer")]
    [InlineData("qa")]
    [InlineData("manager")]
    [InlineData("scrum")]
    public async Task CreatesTaskAsToDoForAnyProjectMemberRoleOrNoAssignee(string? assignee)
    {
        using var fixture = await TaskFixture.CreateAsync();
        var result = await fixture.Tasks.CreateAsync(fixture.CreateRequest(assignee: assignee));

        Assert.True(result.Succeeded, result.Error);
        fixture.Context.ChangeTracker.Clear();
        var saved = await fixture.Context.Set<TaskItem>().SingleAsync();
        Assert.Equal(result.TaskId, saved.Id);
        Assert.Equal(TaskItemStatus.ToDo, saved.Status);
        Assert.Equal("Implement search", saved.Title);
        Assert.Equal("Search the sprint tasks.", saved.Description);
        Assert.Equal(PriorityLevel.High, saved.Priority);
        Assert.Equal(assignee, saved.AssignedUserId);
        Assert.Equal(TaskFixture.Deadline, saved.Deadline);
        Assert.Equal("manager", saved.CreatedByUserId);
        Assert.Equal(TestClock.Now, saved.CreatedAt);
        Assert.Equal(fixture.Link.Id, saved.SprintBacklogItemId);
        Assert.Equal(BacklogItemStatus.InSprint,
            (await fixture.Context.BacklogItems.SingleAsync(x => x.Id == fixture.Backlog.Id)).Status);
    }

    [Theory]
    [InlineData("missing-project")]
    [InlineData("non-scrum")]
    [InlineData("wrong-project")]
    [InlineData("wrong-sprint")]
    [InlineData("missing-sprint")]
    [InlineData("foreign-sprint-backlog")]
    [InlineData("other-sprint-backlog")]
    [InlineData("missing-sprint-backlog")]
    [InlineData("foreign-backlog")]
    [InlineData("backlog-draft")]
    [InlineData("backlog-ready")]
    [InlineData("backlog-completed")]
    public async Task CreateAndEditRejectInvalidProjectSprintAndBacklogRelationships(string scenario)
    {
        using var fixture = await TaskFixture.CreateAsync();
        var projectId = fixture.Project.Id;
        var sprintId = fixture.Sprint.Id;
        var linkId = fixture.Link.Id;
        switch (scenario)
        {
            case "missing-project": projectId = int.MaxValue; break;
            case "non-scrum": fixture.Project.Methodology = ProjectMethodology.VModel; break;
            case "wrong-project": projectId = fixture.OtherProject.Id; break;
            case "wrong-sprint": sprintId = fixture.OtherSprint.Id; break;
            case "missing-sprint": sprintId = int.MaxValue; break;
            case "foreign-sprint-backlog": linkId = fixture.OtherLink.Id; break;
            case "other-sprint-backlog": linkId = fixture.SameProjectOtherLink.Id; break;
            case "missing-sprint-backlog": linkId = int.MaxValue; break;
            case "foreign-backlog": fixture.Backlog.Project = fixture.OtherProject; break;
            case "backlog-draft": fixture.Backlog.Status = BacklogItemStatus.Draft; break;
            case "backlog-ready": fixture.Backlog.Status = BacklogItemStatus.Ready; break;
            case "backlog-completed": fixture.Backlog.Status = BacklogItemStatus.Completed; break;
        }
        await fixture.Context.SaveChangesAsync();

        var create = await fixture.Tasks.CreateAsync(fixture.CreateRequest(projectId, sprintId, linkId));
        Assert.False(create.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(create.Error));
        Assert.False(await fixture.Context.Set<TaskItem>().AnyAsync());

        var original = await fixture.SeedTaskAsync();
        var update = await fixture.Tasks.UpdateAsync(fixture.UpdateRequest(original.Id, projectId, sprintId, linkId));
        Assert.False(update.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(update.Error));
        await fixture.AssertUnchangedAsync(original);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("nonmember")]
    [InlineData("unknown-user")]
    public async Task CreateAndEditRejectAssigneesOutsideTheProject(string assignee)
    {
        using var fixture = await TaskFixture.CreateAsync();
        var create = await fixture.Tasks.CreateAsync(fixture.CreateRequest(assignee: assignee));
        Assert.False(create.Succeeded);
        Assert.False(await fixture.Context.Set<TaskItem>().AnyAsync());

        var original = await fixture.SeedTaskAsync();
        var update = await fixture.Tasks.UpdateAsync(fixture.UpdateRequest(original.Id, assignee: assignee));
        Assert.False(update.Succeeded);
        await fixture.AssertUnchangedAsync(original);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ArchivedProjectsAndCompletedSprintsRejectCreateAndEdit(bool archived)
    {
        using var fixture = await TaskFixture.CreateAsync();
        if (archived) fixture.Project.Status = ProjectStatus.Archived;
        else fixture.Sprint.Status = SprintStatus.Completed;
        await fixture.Context.SaveChangesAsync();

        var create = await fixture.Tasks.CreateAsync(fixture.CreateRequest());
        Assert.False(create.Succeeded);
        Assert.False(await fixture.Context.Set<TaskItem>().AnyAsync());
        var original = await fixture.SeedTaskAsync();
        var update = await fixture.Tasks.UpdateAsync(fixture.UpdateRequest(original.Id));
        Assert.False(update.Succeeded);
        await fixture.AssertUnchangedAsync(original);

        var controller = fixture.Controller("manager", SystemRoles.ProjectManager);
        Assert.IsType<BadRequestObjectResult>(await controller.Create(fixture.Project.Id, fixture.Sprint.Id,
            fixture.Form(), default));
        Assert.IsType<BadRequestObjectResult>(await controller.Edit(fixture.Project.Id, fixture.Sprint.Id,
            original.Id, fixture.Form(original.Id), default));
        var list = Assert.IsType<TaskIndexViewModel>(Assert.IsType<ViewResult>(await controller.Index(
            fixture.Project.Id, fixture.Sprint.Id)).Model);
        Assert.True(list.IsReadOnly);
        Assert.False(list.CanManage);
        Assert.Single(list.Tasks.Items);
        var detail = Assert.IsType<TaskDetailsViewModel>(Assert.IsType<ViewResult>(await controller.Details(
            fixture.Project.Id, fixture.Sprint.Id, original.Id, default)).Model);
        Assert.True(detail.IsReadOnly);
        Assert.False(detail.CanEdit);
        await fixture.AssertUnchangedAsync(original);
    }

    [Theory]
    [InlineData("empty-title")]
    [InlineData("long-title")]
    [InlineData("empty-description")]
    [InlineData("long-description")]
    [InlineData("invalid-priority")]
    public async Task InvalidTaskFieldsRejectCreateAndEditWithoutPartialChanges(string scenario)
    {
        using var fixture = await TaskFixture.CreateAsync();
        var title = scenario switch { "empty-title" => "  ", "long-title" => new string('t', 201), _ => "Valid title" };
        var description = scenario switch { "empty-description" => "  ", "long-description" => new string('d', 3001), _ => "Valid description" };
        var priority = scenario == "invalid-priority" ? (PriorityLevel)999 : PriorityLevel.High;
        Assert.False((await fixture.Tasks.CreateAsync(fixture.CreateRequest(title: title,
            description: description, priority: priority))).Succeeded);
        Assert.False(await fixture.Context.Set<TaskItem>().AnyAsync());
        var original = await fixture.SeedTaskAsync();
        Assert.False((await fixture.Tasks.UpdateAsync(fixture.UpdateRequest(original.Id, title: title,
            description: description, priority: priority))).Succeeded);
        await fixture.AssertUnchangedAsync(original);
    }

    [Theory]
    [InlineData(TaskItemStatus.ToDo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.InReview)]
    [InlineData(TaskItemStatus.Done)]
    public async Task EditChangesOnlyAllowedFieldsAndPreservesStatusAndHistory(TaskItemStatus status)
    {
        using var fixture = await TaskFixture.CreateAsync();
        var original = await fixture.SeedTaskAsync(status);
        var result = await fixture.Tasks.UpdateAsync(fixture.UpdateRequest(original.Id, assignee: "qa"));
        Assert.True(result.Succeeded, result.Error);
        fixture.Context.ChangeTracker.Clear();
        var saved = await fixture.Context.Set<TaskItem>().SingleAsync();
        Assert.Equal("Updated title", saved.Title);
        Assert.Equal("Updated description", saved.Description);
        Assert.Equal(PriorityLevel.Critical, saved.Priority);
        Assert.Equal("qa", saved.AssignedUserId);
        Assert.Equal(TaskFixture.Deadline, saved.Deadline);
        Assert.Equal(status, saved.Status);
        Assert.Equal(original.SprintBacklogItemId, saved.SprintBacklogItemId);
        Assert.Equal(original.CreatedByUserId, saved.CreatedByUserId);
        Assert.Equal(original.CreatedAt, saved.CreatedAt);
        Assert.Equal("manager", saved.UpdatedByUserId);
        Assert.Equal(TestClock.Now, saved.UpdatedAt);
        Assert.Equal(BacklogItemStatus.InSprint,
            (await fixture.Context.BacklogItems.SingleAsync(x => x.Id == fixture.Backlog.Id)).Status);
    }

    [Fact]
    public async Task ReadQueriesScopeTasksToBothProjectAndSprintAndRejectForeignBacklog()
    {
        using var fixture = await TaskFixture.CreateAsync();
        var task = await fixture.SeedTaskAsync();
        fixture.Context.Add(new TaskItem { SprintBacklogItemId = fixture.OtherLink.Id, Title = "Foreign task" });
        fixture.Context.Add(new TaskItem { SprintBacklogItemId = fixture.SameProjectOtherLink.Id, Title = "Other sprint task" });
        await fixture.Context.SaveChangesAsync();
        Assert.Equal(task.Id, Assert.Single((await fixture.Tasks.GetSprintTasksAsync(
            fixture.Project.Id, fixture.Sprint.Id)).Items).Id);
        Assert.Empty((await fixture.Tasks.GetSprintTasksAsync(fixture.OtherProject.Id, fixture.Sprint.Id)).Items);
        Assert.Null(await fixture.Tasks.GetByIdAsync(fixture.OtherProject.Id, fixture.Sprint.Id, task.Id));
        Assert.Null(await fixture.Tasks.GetByIdAsync(fixture.Project.Id, fixture.OtherSprint.Id, task.Id));
        Assert.Null(await fixture.Tasks.GetByIdAsync(fixture.Project.Id, fixture.SameProjectOtherLink.SprintId, task.Id));
        Assert.Empty(await fixture.Tasks.GetBacklogOptionsAsync(fixture.OtherProject.Id, fixture.Sprint.Id));

        var backlog = await fixture.Context.BacklogItems.SingleAsync(x => x.Id == fixture.Backlog.Id);
        backlog.ProjectId = fixture.OtherProject.Id;
        await fixture.Context.SaveChangesAsync();
        Assert.Empty((await fixture.Tasks.GetSprintTasksAsync(fixture.Project.Id, fixture.Sprint.Id)).Items);
        Assert.Null(await fixture.Tasks.GetByIdAsync(fixture.Project.Id, fixture.Sprint.Id, task.Id));
        Assert.Empty(await fixture.Tasks.GetBacklogOptionsAsync(fixture.Project.Id, fixture.Sprint.Id));
    }

    [Fact]
    public async Task TaskListPaginatesClampsPageAndKeepsSearchWithinSprint()
    {
        using var fixture = await TaskFixture.CreateAsync();
        for (var index = 0; index < 45; index++)
            fixture.Context.Add(new TaskItem
            {
                SprintBacklogItemId = fixture.Link.Id,
                Title = index % 2 == 0 ? $"Match {index}" : $"Other {index}",
                CreatedAt = TestClock.Now
            });
        fixture.Context.Add(new TaskItem { SprintBacklogItemId = fixture.OtherLink.Id, Title = "Match foreign" });
        fixture.Context.Add(new TaskItem { SprintBacklogItemId = fixture.SameProjectOtherLink.Id, Title = "Match another sprint" });
        await fixture.Context.SaveChangesAsync();

        var first = await fixture.Tasks.GetSprintTasksAsync(fixture.Project.Id, fixture.Sprint.Id, page: -10);
        var middle = await fixture.Tasks.GetSprintTasksAsync(fixture.Project.Id, fixture.Sprint.Id, page: 2);
        var last = await fixture.Tasks.GetSprintTasksAsync(fixture.Project.Id, fixture.Sprint.Id, page: int.MaxValue);
        Assert.Equal(45, first.TotalCount);
        Assert.Equal(1, first.Page);
        Assert.Equal(20, first.Items.Count);
        Assert.Equal(20, middle.Items.Count);
        Assert.Equal(3, last.Page);
        Assert.Equal(5, last.Items.Count);
        Assert.Equal(45, first.Items.Concat(middle.Items).Concat(last.Items).Select(x => x.Id).Distinct().Count());

        var search = await fixture.Tasks.GetSprintTasksAsync(fixture.Project.Id, fixture.Sprint.Id,
            page: int.MaxValue, search: "  Match  ");
        Assert.Equal(23, search.TotalCount);
        Assert.Equal(2, search.Page);
        Assert.Equal(3, search.Items.Count);
        Assert.All(search.Items, item => Assert.StartsWith("Match ", item.Title));
        var noMatches = await fixture.Tasks.GetSprintTasksAsync(fixture.Project.Id, fixture.Sprint.Id,
            search: "nonexistent-title");
        Assert.Empty(noMatches.Items);
        Assert.Equal(0, noMatches.TotalCount);
        Assert.Equal(1, noMatches.Page);
    }

    [Theory]
    [InlineData("developer", SystemRoles.Developer)]
    [InlineData("qa", SystemRoles.QaTester)]
    public async Task DeveloperAndQaMembersCanViewButCannotCreateOrEdit(string userId, string role)
    {
        using var fixture = await TaskFixture.CreateAsync();
        var task = await fixture.SeedTaskAsync();
        var controller = fixture.Controller(userId, role);
        var list = Assert.IsType<TaskIndexViewModel>(Assert.IsType<ViewResult>(await controller.Index(
            fixture.Project.Id, fixture.Sprint.Id)).Model);
        Assert.False(list.CanManage);
        Assert.Single(list.Tasks.Items);
        var detail = Assert.IsType<TaskDetailsViewModel>(Assert.IsType<ViewResult>(await controller.Details(
            fixture.Project.Id, fixture.Sprint.Id, task.Id, default)).Model);
        Assert.False(detail.CanEdit);
        Assert.IsType<ForbidResult>(await controller.Create(fixture.Project.Id, fixture.Sprint.Id,
            fixture.Link.Id, default));
        Assert.IsType<ForbidResult>(await controller.Create(fixture.Project.Id, fixture.Sprint.Id,
            fixture.Form(), default));
        Assert.IsType<ForbidResult>(await controller.Edit(fixture.Project.Id, fixture.Sprint.Id,
            task.Id, default));
        Assert.IsType<ForbidResult>(await controller.Edit(fixture.Project.Id, fixture.Sprint.Id,
            task.Id, fixture.Form(task.Id), default));
        await fixture.AssertUnchangedAsync(task);
    }

    [Theory]
    [InlineData("developer", SystemRoles.ProjectManager)]
    [InlineData("qa", SystemRoles.ScrumMaster)]
    [InlineData("manager", SystemRoles.ScrumMaster)]
    [InlineData("scrum", SystemRoles.ProjectManager)]
    [InlineData("manager", SystemRoles.Developer)]
    [InlineData("manager", "")]
    [InlineData("foreign", SystemRoles.ProjectManager)]
    public async Task MutationsRequireMatchingGlobalAndProjectManagerRoles(string userId, string role)
    {
        using var fixture = await TaskFixture.CreateAsync();
        var task = await fixture.SeedTaskAsync();
        var controller = fixture.Controller(userId, role);
        Assert.IsType<ForbidResult>(await controller.Create(fixture.Project.Id, fixture.Sprint.Id,
            fixture.Form(), default));
        Assert.IsType<ForbidResult>(await controller.Edit(fixture.Project.Id, fixture.Sprint.Id,
            task.Id, fixture.Form(task.Id), default));
        await fixture.AssertUnchangedAsync(task);
    }

    [Theory]
    [InlineData(SystemRoles.ProjectManager)]
    [InlineData(SystemRoles.ScrumMaster)]
    [InlineData(SystemRoles.Developer)]
    [InlineData(SystemRoles.QaTester)]
    public async Task GlobalRoleDoesNotGrantAccessWithoutProjectMembership(string role)
    {
        using var fixture = await TaskFixture.CreateAsync();
        var task = await fixture.SeedTaskAsync();
        var controller = fixture.Controller("foreign", role);
        Assert.IsType<ForbidResult>(await controller.Index(fixture.Project.Id, fixture.Sprint.Id));
        Assert.IsType<ForbidResult>(await controller.Details(fixture.Project.Id, fixture.Sprint.Id, task.Id, default));
        await fixture.AssertUnchangedAsync(task);
    }

    [Fact]
    public async Task AuthorizedProjectManagerCannotReadOrEditForeignTaskOrSprintIds()
    {
        using var fixture = await TaskFixture.CreateAsync();
        var task = await fixture.SeedTaskAsync();
        var controller = fixture.Controller("manager", SystemRoles.ProjectManager);
        Assert.IsType<NotFoundResult>(await controller.Index(fixture.Project.Id, fixture.OtherSprint.Id));
        Assert.IsType<ForbidResult>(await controller.Index(fixture.OtherProject.Id, fixture.OtherSprint.Id));
        var foreignTask = new TaskItem { SprintBacklogItemId = fixture.OtherLink.Id, Title = "Foreign task" };
        fixture.Context.Add(foreignTask);
        await fixture.Context.SaveChangesAsync();
        var foreignSnapshot = TaskSnapshot.From(foreignTask);
        Assert.IsType<NotFoundResult>(await controller.Details(fixture.Project.Id, fixture.Sprint.Id,
            foreignTask.Id, default));
        Assert.IsType<NotFoundResult>(await controller.Edit(fixture.Project.Id, fixture.Sprint.Id,
            foreignTask.Id, fixture.Form(foreignTask.Id), default));
        Assert.IsType<ForbidResult>(await controller.Edit(fixture.OtherProject.Id, fixture.OtherSprint.Id,
            foreignTask.Id, fixture.Form(foreignTask.Id), default));
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(task, TaskSnapshot.From(await fixture.Context.Set<TaskItem>().SingleAsync(x => x.Id == task.Id)));
        Assert.Equal(foreignSnapshot, TaskSnapshot.From(await fixture.Context.Set<TaskItem>().SingleAsync(x => x.Id == foreignTask.Id)));
    }

    [Theory]
    [InlineData("project")]
    [InlineData("sprint")]
    [InlineData("task")]
    public async Task MutationRejectsFormIdentityThatDisagreesWithRoute(string mismatch)
    {
        using var fixture = await TaskFixture.CreateAsync();
        var controller = fixture.Controller("manager", SystemRoles.ProjectManager);
        var form = fixture.Form();
        if (mismatch == "project") form.ProjectId = fixture.OtherProject.Id;
        else if (mismatch == "sprint") form.SprintId = fixture.OtherSprint.Id;
        else form.Id = 777;
        Assert.IsType<BadRequestResult>(await controller.Create(fixture.Project.Id, fixture.Sprint.Id, form, default));
        Assert.False(await fixture.Context.Set<TaskItem>().AnyAsync());
        var task = await fixture.SeedTaskAsync();
        if (mismatch != "task") form.Id = task.Id;
        Assert.IsType<BadRequestResult>(await controller.Edit(fixture.Project.Id, fixture.Sprint.Id,
            task.Id, form, default));
        await fixture.AssertUnchangedAsync(task);
    }

    [Fact]
    public async Task EditCannotMoveTaskToAnotherBacklogItemEvenWithinSameSprint()
    {
        using var fixture = await TaskFixture.CreateAsync();
        var task = await fixture.SeedTaskAsync();
        var otherLink = new SprintBacklogItem
        {
            SprintId = fixture.Sprint.Id,
            BacklogItem = new BacklogItem { ProjectId = fixture.Project.Id, Title = "Another story", Status = BacklogItemStatus.InSprint }
        };
        fixture.Context.Add(otherLink);
        await fixture.Context.SaveChangesAsync();
        Assert.False((await fixture.Tasks.UpdateAsync(fixture.UpdateRequest(task.Id, linkId: otherLink.Id))).Succeeded);
        var form = fixture.Form(task.Id);
        form.SprintBacklogItemId = otherLink.Id;
        var controller = fixture.Controller("manager", SystemRoles.ProjectManager);
        Assert.IsType<BadRequestObjectResult>(await controller.Edit(fixture.Project.Id, fixture.Sprint.Id,
            task.Id, form, default));
        await fixture.AssertUnchangedAsync(task);
    }

    [Fact]
    public async Task TaskAccessRequiresAuthenticatedIdentityAndRecognizedGlobalRole()
    {
        using var fixture = await TaskFixture.CreateAsync();
        var controller = fixture.Controller("manager", "");
        Assert.IsType<ForbidResult>(await controller.Index(fixture.Project.Id, fixture.Sprint.Id));
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "manager"), new Claim(ClaimTypes.Role, SystemRoles.ProjectManager)]));
        Assert.IsType<ChallengeResult>(await controller.Index(fixture.Project.Id, fixture.Sprint.Id));
        Assert.IsType<ChallengeResult>(await controller.Create(fixture.Project.Id, fixture.Sprint.Id,
            fixture.Form(), default));
        Assert.False(await fixture.Context.Set<TaskItem>().AnyAsync());
    }

    [Theory]
    [InlineData("manager", SystemRoles.ProjectManager)]
    [InlineData("scrum", SystemRoles.ScrumMaster)]
    [InlineData("admin", SystemRoles.Admin)]
    public async Task AuthorizedManagersCanCreateAndEditTasksAssignedToQaMember(string userId, string role)
    {
        using var fixture = await TaskFixture.CreateAsync();
        var controller = fixture.Controller(userId, role);
        var form = fixture.Form();
        form.AssignedUserId = "qa";
        Assert.IsType<RedirectToActionResult>(await controller.Create(fixture.Project.Id, fixture.Sprint.Id,
            form, default));
        var task = await fixture.Context.Set<TaskItem>().SingleAsync();
        Assert.Equal(TaskItemStatus.ToDo, task.Status);
        Assert.Equal(userId, task.CreatedByUserId);
        Assert.Equal("qa", task.AssignedUserId);
        var edit = fixture.Form(task.Id);
        edit.Title = "Manager edit";
        edit.AssignedUserId = "scrum";
        Assert.IsType<RedirectToActionResult>(await controller.Edit(fixture.Project.Id, fixture.Sprint.Id,
            task.Id, edit, default));
        fixture.Context.ChangeTracker.Clear();
        task = await fixture.Context.Set<TaskItem>().SingleAsync();
        Assert.Equal("Manager edit", task.Title);
        Assert.Equal("scrum", task.AssignedUserId);
        Assert.Equal(userId, task.UpdatedByUserId);
        Assert.Equal(TaskItemStatus.ToDo, task.Status);
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("nonmember")]
    [InlineData("admin")]
    public async Task AdminCannotBypassAssigneeProjectMembership(string assignee)
    {
        using var fixture = await TaskFixture.CreateAsync();
        var controller = fixture.Controller("admin", SystemRoles.Admin);
        var form = fixture.Form();
        form.AssignedUserId = assignee;
        Assert.IsType<ViewResult>(await controller.Create(fixture.Project.Id, fixture.Sprint.Id, form, default));
        Assert.False(controller.ModelState.IsValid);
        Assert.False(await fixture.Context.Set<TaskItem>().AnyAsync());
        var original = await fixture.SeedTaskAsync();
        controller = fixture.Controller("admin", SystemRoles.Admin);
        form.Id = original.Id;
        Assert.IsType<ViewResult>(await controller.Edit(fixture.Project.Id, fixture.Sprint.Id,
            original.Id, form, default));
        Assert.False(controller.ModelState.IsValid);
        await fixture.AssertUnchangedAsync(original);
    }

    [Fact]
    public async Task AssigneeOptionsIncludeAllProjectMemberRolesAndExcludeOutsiders()
    {
        using var fixture = await TaskFixture.CreateAsync();
        var options = await fixture.Tasks.GetAssigneeOptionsAsync(fixture.Project.Id);
        Assert.Equal(new[] { "developer", "manager", "qa", "scrum" },
            options.Select(x => x.UserId).OrderBy(x => x).ToArray());
        Assert.Equal(Enum.GetValues<ProjectMemberRole>().OrderBy(x => x), options.Select(x => x.Role).OrderBy(x => x));
        Assert.All(options, option => Assert.False(string.IsNullOrWhiteSpace(option.DisplayName)));
    }

    [Fact]
    public async Task SprintBacklogSummaryKeepsBacklogIdAndExposesSeparateJoinIdForTasks()
    {
        using var fixture = await TaskFixture.CreateAsync();
        var service = new SprintService(fixture.Context, new TestClock());
        var summary = Assert.Single(await service.GetSprintBacklogAsync(fixture.Project.Id, fixture.Sprint.Id));
        Assert.NotEqual(fixture.Backlog.Id, fixture.Link.Id);
        Assert.Equal(fixture.Backlog.Id, summary.Id);
        Assert.Equal(fixture.Link.Id, summary.SprintBacklogItemId);
        var removal = await service.RemoveBacklogItemAsync(fixture.Project.Id, fixture.Sprint.Id,
            summary.Id, "manager");
        Assert.True(removal.Succeeded);
        Assert.False(await fixture.Context.SprintBacklogItems.AnyAsync(x => x.Id == fixture.Link.Id));
        Assert.Equal(BacklogItemStatus.Ready,
            (await fixture.Context.BacklogItems.SingleAsync(x => x.Id == fixture.Backlog.Id)).Status);
    }

    [Fact]
    public void TaskEndpointsRequireAuthenticationAndMutationPostsRequireAntiforgery()
    {
        Assert.NotNull(typeof(TasksController).GetCustomAttribute<AuthorizeAttribute>());
        var posts = typeof(TasksController).GetMethods()
            .Where(method => method.GetCustomAttribute<HttpPostAttribute>() is not null).ToArray();
        Assert.Equal(new[]
            {
                "AddComment", "Create", "DeleteAttachment", "DeleteComment",
                "Edit", "QaReview", "UpdateStatus", "UploadAttachment"
            },
            posts.Select(method => method.Name).OrderBy(name => name).ToArray());
        Assert.All(posts, method => Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
        Assert.Null(typeof(TaskFormViewModel).GetProperty("Status"));
        Assert.Null(typeof(CreateTaskRequest).GetProperty("Status"));
        Assert.Null(typeof(UpdateTaskRequest).GetProperty("Status"));
    }

    private sealed record TaskSnapshot(int Id, int SprintBacklogItemId, string Title, string Description,
        PriorityLevel Priority, TaskItemStatus Status, string? AssignedUserId, DateTime? Deadline,
        string CreatedByUserId, string? UpdatedByUserId, DateTime CreatedAt, DateTime? UpdatedAt)
    {
        public static TaskSnapshot From(TaskItem task) => new(task.Id, task.SprintBacklogItemId, task.Title,
            task.Description, task.Priority, task.Status, task.AssignedUserId, task.Deadline,
            task.CreatedByUserId, task.UpdatedByUserId, task.CreatedAt, task.UpdatedAt);
    }

    private sealed class TaskFixture : IDisposable
    {
        public static readonly DateTime Deadline = new(2026, 9, 20);
        public ApplicationDbContext Context { get; }
        public TaskService Tasks { get; }
        public Project Project { get; } = new() { Id = 1, Name = "Scrum project", Methodology = ProjectMethodology.Scrum, Status = ProjectStatus.Active };
        public Project OtherProject { get; } = new() { Id = 2, Name = "Other project", Methodology = ProjectMethodology.Scrum, Status = ProjectStatus.Active };
        public Sprint Sprint { get; }
        public Sprint OtherSprint { get; }
        public BacklogItem Backlog { get; }
        public SprintBacklogItem Link { get; }
        public SprintBacklogItem OtherLink { get; }
        public SprintBacklogItem SameProjectOtherLink { get; }

        private TaskFixture()
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Tasks = new TaskService(Context, new TaskAssignmentService(Context), new TestClock());
            Sprint = new Sprint { Id = 11, Project = Project, Name = "Target sprint", Status = SprintStatus.InProgress };
            OtherSprint = new Sprint { Id = 12, Project = OtherProject, Name = "Foreign sprint" };
            Backlog = new BacklogItem { Id = 101, Project = Project, Title = "Search", Status = BacklogItemStatus.InSprint };
            Link = new SprintBacklogItem { Id = 1001, Sprint = Sprint, BacklogItem = Backlog };
            OtherLink = new SprintBacklogItem
            {
                Id = 1002, Sprint = OtherSprint,
                BacklogItem = new BacklogItem { Id = 102, Project = OtherProject, Title = "Foreign backlog", Status = BacklogItemStatus.InSprint }
            };
            SameProjectOtherLink = new SprintBacklogItem
            {
                Id = 1003, Sprint = new Sprint { Id = 13, Project = Project, Name = "Another sprint" },
                BacklogItem = new BacklogItem { Id = 103, Project = Project, Title = "Another backlog", Status = BacklogItemStatus.InSprint }
            };
        }

        public static async Task<TaskFixture> CreateAsync()
        {
            var fixture = new TaskFixture();
            fixture.Context.AddRange(fixture.Link, fixture.OtherLink, fixture.SameProjectOtherLink);
            foreach (var (id, role) in new[]
            {
                ("manager", ProjectMemberRole.ProjectManager), ("scrum", ProjectMemberRole.ScrumMaster),
                ("developer", ProjectMemberRole.Developer), ("qa", ProjectMemberRole.QaTester)
            })
            {
                fixture.Context.Users.Add(new ApplicationUser { Id = id, UserName = id, FullName = id + " user" });
                fixture.Context.ProjectMembers.Add(new ProjectMember { Project = fixture.Project, UserId = id, Role = role });
            }
            foreach (var id in new[] { "foreign", "nonmember", "admin" })
                fixture.Context.Users.Add(new ApplicationUser { Id = id, UserName = id, FullName = id + " user" });
            fixture.Context.ProjectMembers.Add(new ProjectMember
                { Project = fixture.OtherProject, UserId = "foreign", Role = ProjectMemberRole.ProjectManager });
            await fixture.Context.SaveChangesAsync();
            return fixture;
        }

        public CreateTaskRequest CreateRequest(int? projectId = null, int? sprintId = null, int? linkId = null,
            string? assignee = "developer", string title = "Implement search", string description = "Search the sprint tasks.",
            PriorityLevel priority = PriorityLevel.High) => new()
        {
            ProjectId = projectId ?? Project.Id, SprintId = sprintId ?? Sprint.Id,
            SprintBacklogItemId = linkId ?? Link.Id, Title = title, Description = description,
            Priority = priority, AssignedUserId = assignee, Deadline = Deadline, CreatedByUserId = "manager"
        };

        public UpdateTaskRequest UpdateRequest(int taskId, int? projectId = null, int? sprintId = null, int? linkId = null,
            string? assignee = "developer", string title = "Updated title", string description = "Updated description",
            PriorityLevel priority = PriorityLevel.Critical) => new()
        {
            Id = taskId, ProjectId = projectId ?? Project.Id, SprintId = sprintId ?? Sprint.Id,
            SprintBacklogItemId = linkId ?? Link.Id, Title = title, Description = description,
            Priority = priority, AssignedUserId = assignee, Deadline = Deadline, UpdatedByUserId = "manager"
        };

        public TaskFormViewModel Form(int id = 0) => new()
        {
            Id = id, ProjectId = Project.Id, SprintId = Sprint.Id, SprintBacklogItemId = Link.Id,
            Title = "Task form title", Description = "Task form description", Priority = PriorityLevel.High,
            AssignedUserId = "developer", Deadline = Deadline
        };

        public async Task<TaskSnapshot> SeedTaskAsync(TaskItemStatus status = TaskItemStatus.InReview)
        {
            var task = new TaskItem
            {
                SprintBacklogItemId = Link.Id, Title = "Original title", Description = "Original description",
                Priority = PriorityLevel.Low, Status = status, AssignedUserId = "developer",
                Deadline = Deadline.AddDays(-1), CreatedByUserId = "original-author", CreatedAt = TestClock.Now.AddDays(-5)
            };
            Context.Add(task);
            await Context.SaveChangesAsync();
            return TaskSnapshot.From(task);
        }

        public async Task AssertUnchangedAsync(TaskSnapshot original)
        {
            Context.ChangeTracker.Clear();
            Assert.Equal(original, TaskSnapshot.From(await Context.Set<TaskItem>().SingleAsync()));
        }

        public TasksController Controller(string userId, string role)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
            if (!string.IsNullOrWhiteSpace(role)) claims.Add(new Claim(ClaimTypes.Role, role));
            var httpContext = new DefaultHttpContext
                { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) };
            return new TasksController(Tasks, new ProjectService(Context, new TestClock()),
                new SprintService(Context, new TestClock()), new ProjectAccessService(Context),
                NullLogger<TasksController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext },
                TempData = new TempDataDictionary(httpContext, new TestTempDataProvider())
            };
        }

        public void Dispose() => Context.Dispose();
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class TestClock : IClock
    {
        public static readonly DateTime Now = new(2026, 9, 6, 0, 0, 0, DateTimeKind.Utc);
        public DateTime UtcNow => Now;
    }
}
