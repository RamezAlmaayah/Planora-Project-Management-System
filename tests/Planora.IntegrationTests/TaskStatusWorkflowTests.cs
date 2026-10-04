using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

public sealed class TaskStatusWorkflowTests
{
    [Theory]
    [InlineData(TaskItemStatus.ToDo, TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.InReview)]
    public async Task AllowedStatusTransitionsSucceed(
        TaskItemStatus current, TaskItemStatus target)
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(current, "developer");

        var result = await fixture.Tasks.TransitionStatusAsync(
            fixture.Request(task, current, target, "developer", TaskTransitionActor.Developer));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(target, result.Status);
        fixture.Context.ChangeTracker.Clear();
        var saved = await fixture.Context.TaskItems.SingleAsync();
        Assert.Equal(target, saved.Status);
        Assert.Equal("developer", saved.UpdatedByUserId);
        Assert.Equal(TestClock.Now, saved.UpdatedAt);
    }

    [Theory]
    [InlineData(TaskItemStatus.ToDo, TaskItemStatus.InReview)]
    [InlineData(TaskItemStatus.ToDo, TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.InProgress, TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.InReview, TaskItemStatus.Done)]
    [InlineData(TaskItemStatus.InReview, TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Done, TaskItemStatus.ToDo)]
    public async Task DisallowedStatusTransitionsAreRejected(
        TaskItemStatus current, TaskItemStatus target)
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(current, "developer");

        var result = await fixture.Tasks.TransitionStatusAsync(
            fixture.Request(task, current, target, "developer", TaskTransitionActor.Developer));

        Assert.False(result.Succeeded);
        Assert.Equal(TaskStatusTransitionFailure.Invalid, result.Failure);
        Assert.Equal(current, (await fixture.Context.TaskItems.SingleAsync()).Status);
    }

    [Fact]
    public async Task DeveloperCannotTransitionAnotherUsersTask()
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.ToDo, "other-developer");

        var result = await fixture.Tasks.TransitionStatusAsync(fixture.Request(
            task, TaskItemStatus.ToDo, TaskItemStatus.InProgress,
            "developer", TaskTransitionActor.Developer));

        Assert.Equal(TaskStatusTransitionFailure.Forbidden, result.Failure);
        Assert.Equal(TaskItemStatus.ToDo, (await fixture.Context.TaskItems.SingleAsync()).Status);
    }

    [Theory]
    [InlineData(TaskTransitionActor.Developer)]
    [InlineData(TaskTransitionActor.ProjectManager)]
    [InlineData(TaskTransitionActor.ScrumMaster)]
    public async Task NonMemberOrMismatchedProjectRoleIsRejected(TaskTransitionActor actor)
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.ToDo, "outsider");

        var result = await fixture.Tasks.TransitionStatusAsync(fixture.Request(
            task, TaskItemStatus.ToDo, TaskItemStatus.InProgress, "outsider", actor));

        Assert.Equal(TaskStatusTransitionFailure.Forbidden, result.Failure);
        Assert.Equal(TaskItemStatus.ToDo, (await fixture.Context.TaskItems.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("foreign-task")]
    [InlineData("wrong-sprint")]
    [InlineData("wrong-project")]
    public async Task CrossProjectTaskAndWrongSprintAreRejected(string scenario)
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        var task = scenario == "foreign-task"
            ? await fixture.AddForeignTaskAsync()
            : await fixture.AddTaskAsync(TaskItemStatus.ToDo, "manager");
        int projectId = scenario == "wrong-project" ? fixture.OtherProject.Id : fixture.Project.Id;
        int sprintId = scenario == "wrong-sprint" ? fixture.OtherSprint.Id : fixture.Sprint.Id;

        var result = await fixture.Tasks.TransitionStatusAsync(new TaskStatusTransitionRequest
        {
            ProjectId = projectId,
            SprintId = sprintId,
            TaskId = task.Id,
            CurrentStatus = TaskItemStatus.ToDo,
            TargetStatus = TaskItemStatus.InProgress,
            ActorUserId = "manager",
            Actor = TaskTransitionActor.ProjectManager
        });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failure,
            new[] { TaskStatusTransitionFailure.NotFound, TaskStatusTransitionFailure.Forbidden });
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(TaskItemStatus.ToDo,
            (await fixture.Context.TaskItems.SingleAsync(item => item.Id == task.Id)).Status);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ArchivedProjectAndCompletedSprintAreReadOnly(bool archived, bool completed)
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.ToDo, "developer");
        if (archived) fixture.Project.Status = ProjectStatus.Archived;
        if (completed) fixture.Sprint.Status = SprintStatus.Completed;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Tasks.TransitionStatusAsync(fixture.Request(
            task, TaskItemStatus.ToDo, TaskItemStatus.InProgress,
            "developer", TaskTransitionActor.Developer));

        Assert.Equal(TaskStatusTransitionFailure.ReadOnly, result.Failure);
        Assert.Equal(TaskItemStatus.ToDo, (await fixture.Context.TaskItems.SingleAsync()).Status);

        var boardResult = Assert.IsType<ViewResult>(await fixture.Controller(
            "developer", SystemRoles.Developer).Board(fixture.Project.Id, fixture.Sprint.Id, default));
        var board = Assert.IsType<TaskBoardViewModel>(boardResult.Model);
        Assert.True(board.IsReadOnly);
        Assert.All(board.Tasks, item => Assert.False(item.CanTransition));
    }

    [Fact]
    public async Task DuplicateOrStaleTransitionIsRejectedAsConflict()
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.ToDo, "developer");
        var request = fixture.Request(task, TaskItemStatus.ToDo, TaskItemStatus.InProgress,
            "developer", TaskTransitionActor.Developer);

        Assert.True((await fixture.Tasks.TransitionStatusAsync(request)).Succeeded);
        var duplicate = await fixture.Tasks.TransitionStatusAsync(request);

        Assert.False(duplicate.Succeeded);
        Assert.Equal(TaskStatusTransitionFailure.Conflict, duplicate.Failure);
        Assert.Equal(TaskItemStatus.InProgress, (await fixture.Context.TaskItems.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("admin", SystemRoles.Admin, true)]
    [InlineData("manager", SystemRoles.ProjectManager, true)]
    [InlineData("scrum", SystemRoles.ScrumMaster, true)]
    [InlineData("developer", SystemRoles.Developer, true)]
    [InlineData("qa", SystemRoles.QaTester, false)]
    public async Task AuthorizedMembersCanViewBoardWithRoleAppropriateActions(
        string userId, string globalRole, bool expectedCanTransition)
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        await fixture.AddTaskAsync(TaskItemStatus.ToDo, "developer");

        var result = Assert.IsType<ViewResult>(await fixture.Controller(userId, globalRole)
            .Board(fixture.Project.Id, fixture.Sprint.Id, default));
        var model = Assert.IsType<TaskBoardViewModel>(result.Model);

        Assert.False(model.IsReadOnly);
        Assert.Equal(expectedCanTransition, Assert.Single(model.Tasks).CanTransition);
    }

    [Fact]
    public async Task ProjectMemberDeveloperCannotMoveTaskUnlessAssignedToThem()
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        await fixture.AddTaskAsync(TaskItemStatus.ToDo, "other-developer");

        var model = Assert.IsType<TaskBoardViewModel>(Assert.IsType<ViewResult>(
            await fixture.Controller("developer", SystemRoles.Developer)
                .Board(fixture.Project.Id, fixture.Sprint.Id, default)).Model);

        Assert.False(Assert.Single(model.Tasks).CanTransition);
    }

    [Theory]
    [InlineData("outsider", SystemRoles.Developer)]
    [InlineData("other-manager", SystemRoles.ProjectManager)]
    public async Task UnauthorizedBoardAccessIsRejected(string userId, string globalRole)
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        await fixture.AddTaskAsync(TaskItemStatus.ToDo, "developer");

        Assert.IsType<ForbidResult>(await fixture.Controller(userId, globalRole)
            .Board(fixture.Project.Id, fixture.Sprint.Id, default));
    }

    [Fact]
    public async Task QaCannotUseStatusEndpoint()
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview, "developer");

        var result = Assert.IsType<ObjectResult>(await fixture.Controller("qa", SystemRoles.QaTester)
            .UpdateStatus(new TaskStatusUpdateViewModel
            {
                ProjectId = fixture.Project.Id,
                SprintId = fixture.Sprint.Id,
                TaskId = task.Id,
                CurrentStatus = TaskItemStatus.InReview,
                TargetStatus = TaskItemStatus.Done
            }, default));

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal(TaskItemStatus.InReview, (await fixture.Context.TaskItems.SingleAsync()).Status);
    }

    [Theory]
    [InlineData("manager", SystemRoles.ProjectManager)]
    [InlineData("scrum", SystemRoles.ScrumMaster)]
    [InlineData("admin", SystemRoles.Admin)]
    public async Task ManagersCanUseAllowedTransitionsButCannotMoveInReviewToDone(
        string userId, string globalRole)
    {
        using var fixture = await WorkflowFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.ToDo, "developer");
        var controller = fixture.Controller(userId, globalRole);

        var start = await controller.UpdateStatus(fixture.Model(
            task, TaskItemStatus.ToDo, TaskItemStatus.InProgress), default);
        Assert.IsType<OkObjectResult>(start);
        var review = await controller.UpdateStatus(fixture.Model(
            task, TaskItemStatus.InProgress, TaskItemStatus.InReview), default);
        Assert.IsType<OkObjectResult>(review);
        var done = Assert.IsType<ObjectResult>(await controller.UpdateStatus(fixture.Model(
            task, TaskItemStatus.InReview, TaskItemStatus.Done), default));

        Assert.Equal(StatusCodes.Status400BadRequest, done.StatusCode);
        Assert.Equal(TaskItemStatus.InReview, (await fixture.Context.TaskItems.SingleAsync()).Status);
    }

    private sealed class WorkflowFixture : IDisposable
    {
        public ApplicationDbContext Context { get; }
        public TaskService Tasks { get; }
        public Project Project { get; } = new()
            { Id = 1, Name = "Scrum", Methodology = ProjectMethodology.Scrum, Status = ProjectStatus.Active };
        public Project OtherProject { get; } = new()
            { Id = 2, Name = "Other", Methodology = ProjectMethodology.Scrum, Status = ProjectStatus.Active };
        public Sprint Sprint { get; }
        public Sprint OtherSprint { get; }
        private Sprint ForeignSprint { get; }
        private SprintBacklogItem Link { get; }
        private SprintBacklogItem ForeignLink { get; }

        private WorkflowFixture()
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Tasks = new TaskService(Context, new TaskAssignmentService(Context), new TestClock());
            Sprint = new Sprint { Id = 11, Project = Project, Name = "Sprint", Status = SprintStatus.InProgress };
            OtherSprint = new Sprint { Id = 12, Project = Project, Name = "Other sprint", Status = SprintStatus.InProgress };
            ForeignSprint = new Sprint { Id = 21, Project = OtherProject, Name = "Foreign", Status = SprintStatus.InProgress };
            Link = new SprintBacklogItem
            {
                Id = 101,
                Sprint = Sprint,
                BacklogItem = new BacklogItem
                    { Id = 201, Project = Project, Title = "Story", Status = BacklogItemStatus.InSprint }
            };
            ForeignLink = new SprintBacklogItem
            {
                Id = 102,
                Sprint = ForeignSprint,
                BacklogItem = new BacklogItem
                    { Id = 202, Project = OtherProject, Title = "Foreign", Status = BacklogItemStatus.InSprint }
            };
        }

        public static async Task<WorkflowFixture> CreateAsync()
        {
            var fixture = new WorkflowFixture();
            fixture.Context.AddRange(fixture.Link, fixture.ForeignLink, fixture.OtherSprint);
            foreach (string userId in new[]
                     { "admin", "manager", "scrum", "developer", "other-developer", "qa", "outsider", "other-manager" })
            {
                fixture.Context.Users.Add(new ApplicationUser
                    { Id = userId, UserName = userId, FullName = userId });
            }
            fixture.Context.ProjectMembers.AddRange(
                new ProjectMember { Project = fixture.Project, UserId = "manager", Role = ProjectMemberRole.ProjectManager },
                new ProjectMember { Project = fixture.Project, UserId = "scrum", Role = ProjectMemberRole.ScrumMaster },
                new ProjectMember { Project = fixture.Project, UserId = "developer", Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = fixture.Project, UserId = "other-developer", Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = fixture.Project, UserId = "qa", Role = ProjectMemberRole.QaTester },
                new ProjectMember { Project = fixture.OtherProject, UserId = "other-manager", Role = ProjectMemberRole.ProjectManager });
            await fixture.Context.SaveChangesAsync();
            return fixture;
        }

        public async Task<TaskItem> AddTaskAsync(TaskItemStatus status, string? assignee)
        {
            var task = new TaskItem
            {
                SprintBacklogItemId = Link.Id,
                Title = "Task",
                Description = "Description",
                Status = status,
                AssignedUserId = assignee,
                CreatedByUserId = "manager",
                CreatedAt = TestClock.Now.AddDays(-1)
            };
            Context.Add(task);
            await Context.SaveChangesAsync();
            return task;
        }

        public async Task<TaskItem> AddForeignTaskAsync()
        {
            var task = new TaskItem
            {
                SprintBacklogItemId = ForeignLink.Id,
                Title = "Foreign task",
                Description = "Description",
                Status = TaskItemStatus.ToDo,
                AssignedUserId = "other-manager",
                CreatedByUserId = "other-manager"
            };
            Context.Add(task);
            await Context.SaveChangesAsync();
            return task;
        }

        public TaskStatusTransitionRequest Request(TaskItem task, TaskItemStatus current,
            TaskItemStatus target, string userId, TaskTransitionActor actor) => new()
        {
            ProjectId = Project.Id,
            SprintId = Sprint.Id,
            TaskId = task.Id,
            CurrentStatus = current,
            TargetStatus = target,
            ActorUserId = userId,
            Actor = actor
        };

        public TaskStatusUpdateViewModel Model(
            TaskItem task, TaskItemStatus current, TaskItemStatus target) => new()
        {
            ProjectId = Project.Id,
            SprintId = Sprint.Id,
            TaskId = task.Id,
            CurrentStatus = current,
            TargetStatus = target
        };

        public TasksController Controller(string userId, string globalRole)
        {
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId),
                    new Claim(ClaimTypes.Role, globalRole)
                }, "Test"))
            };
            return new TasksController(
                Tasks,
                new ProjectService(Context, new TestClock()),
                new SprintService(Context, new TestClock()),
                new ProjectAccessService(Context),
                NullLogger<TasksController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = httpContext }
            };
        }

        public void Dispose() => Context.Dispose();
    }

    private sealed class TestClock : IClock
    {
        public static readonly DateTime Now = new(2026, 9, 6, 12, 0, 0, DateTimeKind.Utc);
        public DateTime UtcNow => Now;
    }
}
