using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Security;
using Planora.Application.Common.Tasks;
using Planora.Application.Abstractions.Tasks;
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

public sealed class QaReviewWorkflowTests
{
    [Fact]
    public async Task QaMemberCanPassInReviewTaskAndHistoryIsCreated()
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);

        var result = await fixture.Tasks.ReviewAsync(fixture.Review(task, QaReviewResult.Pass));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(TaskItemStatus.Done, result.TaskStatus);
        Assert.Equal(BacklogItemStatus.Completed, result.BacklogStatus);
        fixture.Context.ChangeTracker.Clear();
        var savedTask = await fixture.Context.TaskItems.SingleAsync();
        var review = await fixture.Context.QaReviews.SingleAsync();
        Assert.Equal(TaskItemStatus.Done, savedTask.Status);
        Assert.Equal("qa", savedTask.UpdatedByUserId);
        Assert.Equal(task.Id, review.TaskItemId);
        Assert.Equal("qa", review.QaUserId);
        Assert.Equal(QaReviewResult.Pass, review.Result);
        Assert.Null(review.Notes);
        Assert.Equal(TestClock.Now, review.TestedAt);
    }

    [Fact]
    public async Task QaMemberCanFailInReviewTaskWithMandatoryNote()
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);

        var missingNote = await fixture.Tasks.ReviewAsync(
            fixture.Review(task, QaReviewResult.Fail, "  "));
        Assert.False(missingNote.Succeeded);
        Assert.Equal(QaReviewOperationFailure.Invalid, missingNote.Failure);
        Assert.Empty(fixture.Context.QaReviews);
        Assert.Equal(TaskItemStatus.InReview, task.Status);

        var result = await fixture.Tasks.ReviewAsync(
            fixture.Review(task, QaReviewResult.Fail, "Acceptance criteria are not satisfied."));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(TaskItemStatus.InProgress, result.TaskStatus);
        var review = await fixture.Context.QaReviews.SingleAsync();
        Assert.Equal(QaReviewResult.Fail, review.Result);
        Assert.Equal("Acceptance criteria are not satisfied.", review.Notes);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Equal(BacklogItemStatus.InSprint, fixture.Backlog.Status);
    }

    [Fact]
    public async Task QaReviewStoresEvidenceOnTheExactReviewAndCleansItOnPersistenceFailure()
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);
        byte[] png = [137, 80, 78, 71, 13, 10, 26, 10];
        var request = WithEvidence(fixture.Review(task, QaReviewResult.Pass), new QaEvidenceUpload
        {
            OriginalFileName = @"C:\fakepath\result.png",
            ContentType = "image/png",
            FileSizeBytes = png.Length,
            Content = new MemoryStream(png)
        });

        var result = await fixture.Tasks.ReviewAsync(request);

        Assert.True(result.Succeeded, result.Message);
        var evidence = await fixture.Context.QaEvidenceFiles.SingleAsync();
        Assert.Equal(result.ReviewId, evidence.QaReviewId);
        Assert.Equal("result.png", evidence.OriginalFileName);
        Assert.Equal("qa", evidence.UploadedByUserId);
        Assert.StartsWith("qa-", evidence.StorageKey);
        Assert.Equal(png, fixture.Storage.Files[evidence.StorageKey]);
        var details = await fixture.Tasks.GetByIdAsync(fixture.Project.Id, fixture.Sprint.Id, task.Id);
        Assert.NotNull(details);
        Assert.Single(Assert.Single(details.QaReviews).EvidenceFiles);
    }

    [Fact]
    public async Task QaReviewRejectsEvidenceWithUnsupportedExtensionOrMismatchedSignature()
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);
        var request = WithEvidence(fixture.Review(task, QaReviewResult.Pass), new QaEvidenceUpload
        {
            OriginalFileName = "payload.png",
            ContentType = "image/png",
            FileSizeBytes = 3,
            Content = new MemoryStream([1, 2, 3])
        });

        var result = await fixture.Tasks.ReviewAsync(request);

        Assert.False(result.Succeeded);
        Assert.Empty(fixture.Context.QaReviews);
        Assert.Empty(fixture.Context.QaEvidenceFiles);
        Assert.Empty(fixture.Storage.Files);
        Assert.Equal(TaskItemStatus.InReview, task.Status);
    }

    [Theory]
    [InlineData("developer", SystemRoles.Developer)]
    [InlineData("manager", SystemRoles.ProjectManager)]
    [InlineData("scrum", SystemRoles.ScrumMaster)]
    [InlineData("admin", SystemRoles.Admin)]
    public async Task NonQaGlobalRolesCannotPassOrFail(string userId, string role)
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);
        var controller = fixture.Controller(userId, role);

        foreach (QaReviewResult reviewResult in Enum.GetValues<QaReviewResult>())
        {
            var result = Assert.IsType<ObjectResult>(await controller.QaReview(
                fixture.Model(task, reviewResult, reviewResult == QaReviewResult.Fail ? "Bug remains." : null),
                default));
            Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        }

        Assert.Empty(fixture.Context.QaReviews);
        Assert.Equal(TaskItemStatus.InReview, task.Status);
    }

    [Fact]
    public async Task AdminWithQaGlobalRoleStillCannotPerformQa()
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);

        var result = Assert.IsType<ObjectResult>(await fixture.Controller(
            "admin", SystemRoles.Admin, SystemRoles.QaTester).QaReview(
                fixture.Model(task, QaReviewResult.Pass), default));

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Empty(fixture.Context.QaReviews);
        Assert.Equal(TaskItemStatus.InReview, task.Status);
    }

    [Theory]
    [InlineData("qa-nonmember")]
    [InlineData("qa-wrong-role")]
    public async Task QaRequiresMatchingProjectMembershipRole(string qaUserId)
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);
        var serviceResult = await fixture.Tasks.ReviewAsync(new QaReviewTaskRequest
        {
            ProjectId = fixture.Project.Id,
            SprintId = fixture.Sprint.Id,
            TaskId = task.Id,
            ReviewerUserId = qaUserId,
            Result = QaReviewResult.Pass
        });
        Assert.Equal(QaReviewOperationFailure.Forbidden, serviceResult.Failure);

        var action = Assert.IsType<ObjectResult>(await fixture.Controller(
            qaUserId, SystemRoles.QaTester).QaReview(fixture.Model(task, QaReviewResult.Pass), default));
        Assert.Equal(StatusCodes.Status403Forbidden, action.StatusCode);
        Assert.Empty(fixture.Context.QaReviews);
        Assert.Equal(TaskItemStatus.InReview, task.Status);
    }

    [Theory]
    [InlineData("foreign-task")]
    [InlineData("wrong-sprint")]
    [InlineData("wrong-project")]
    public async Task CrossProjectTaskAndWrongSprintAreRejected(string scenario)
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = scenario == "foreign-task"
            ? await fixture.AddForeignTaskAsync()
            : await fixture.AddTaskAsync(TaskItemStatus.InReview);
        int projectId = scenario == "wrong-project" ? fixture.OtherProject.Id : fixture.Project.Id;
        int sprintId = scenario == "wrong-sprint" ? fixture.OtherSprint.Id : fixture.Sprint.Id;

        var result = await fixture.Tasks.ReviewAsync(new QaReviewTaskRequest
        {
            ProjectId = projectId,
            SprintId = sprintId,
            TaskId = task.Id,
            ReviewerUserId = "qa",
            Result = QaReviewResult.Pass
        });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Failure,
            new[] { QaReviewOperationFailure.NotFound, QaReviewOperationFailure.Forbidden });
        Assert.Empty(fixture.Context.QaReviews);
        fixture.Context.ChangeTracker.Clear();
        Assert.Equal(TaskItemStatus.InReview,
            (await fixture.Context.TaskItems.SingleAsync(item => item.Id == task.Id)).Status);
    }

    [Theory]
    [InlineData(TaskItemStatus.ToDo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Done)]
    public async Task QaCanReviewOnlyInReviewTasks(TaskItemStatus status)
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(status);

        var result = await fixture.Tasks.ReviewAsync(fixture.Review(task, QaReviewResult.Pass));

        Assert.Equal(QaReviewOperationFailure.Conflict, result.Failure);
        Assert.Empty(fixture.Context.QaReviews);
        Assert.Equal(status, task.Status);
    }

    [Fact]
    public async Task DuplicateOrStaleQaRequestCreatesOnlyOneReview()
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);
        var request = fixture.Review(task, QaReviewResult.Pass);

        Assert.True((await fixture.Tasks.ReviewAsync(request)).Succeeded);
        var duplicate = await fixture.Tasks.ReviewAsync(request);

        Assert.Equal(QaReviewOperationFailure.Conflict, duplicate.Failure);
        Assert.Single(fixture.Context.QaReviews);
        Assert.Equal(TaskItemStatus.Done, task.Status);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ArchivedProjectAndCompletedSprintRejectQa(bool archived, bool completed)
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);
        if (archived) fixture.Project.Status = ProjectStatus.Archived;
        if (completed) fixture.Sprint.Status = SprintStatus.Completed;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Tasks.ReviewAsync(fixture.Review(task, QaReviewResult.Pass));

        Assert.Equal(QaReviewOperationFailure.ReadOnly, result.Failure);
        Assert.Empty(fixture.Context.QaReviews);
        Assert.Equal(TaskItemStatus.InReview, task.Status);
    }

    [Fact]
    public async Task PassingOneTaskKeepsBacklogActiveWhenAnotherTaskIsUnfinished()
    {
        using var fixture = await QaFixture.CreateAsync();
        var reviewed = await fixture.AddTaskAsync(TaskItemStatus.InReview);
        await fixture.AddTaskAsync(TaskItemStatus.ToDo);

        var result = await fixture.Tasks.ReviewAsync(fixture.Review(reviewed, QaReviewResult.Pass));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(BacklogItemStatus.InSprint, result.BacklogStatus);
        Assert.Equal(BacklogItemStatus.InSprint, fixture.Backlog.Status);
    }

    [Fact]
    public async Task PassingLastTaskCompletesBacklogAcrossSprintAssignments()
    {
        using var fixture = await QaFixture.CreateAsync();
        var reviewed = await fixture.AddTaskAsync(TaskItemStatus.InReview);
        var secondLink = new SprintBacklogItem
        {
            Sprint = fixture.OtherSprint,
            BacklogItem = fixture.Backlog,
            AddedByUserId = "manager"
        };
        fixture.Context.Add(secondLink);
        await fixture.Context.SaveChangesAsync();
        await fixture.AddTaskAsync(TaskItemStatus.Done, secondLink);

        var result = await fixture.Tasks.ReviewAsync(fixture.Review(reviewed, QaReviewResult.Pass));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(BacklogItemStatus.Completed, result.BacklogStatus);
        Assert.Equal(BacklogItemStatus.Completed, fixture.Backlog.Status);
    }

    [Fact]
    public async Task ZeroTaskBacklogIsNeverCompletedByAnotherTasksReview()
    {
        using var fixture = await QaFixture.CreateAsync();
        var untouched = new BacklogItem
        {
            Project = fixture.Project,
            Title = "No tasks",
            Description = "No tasks",
            Status = BacklogItemStatus.InSprint
        };
        fixture.Context.Add(untouched);
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);

        Assert.True((await fixture.Tasks.ReviewAsync(
            fixture.Review(task, QaReviewResult.Pass))).Succeeded);

        Assert.Equal(BacklogItemStatus.InSprint, untouched.Status);
    }

    [Fact]
    public async Task QaFailRestoresCompletedBacklogToInSprint()
    {
        using var fixture = await QaFixture.CreateAsync();
        fixture.Backlog.Status = BacklogItemStatus.Completed;
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);

        var result = await fixture.Tasks.ReviewAsync(
            fixture.Review(task, QaReviewResult.Fail, "Validation failed."));

        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(TaskItemStatus.InProgress, task.Status);
        Assert.Equal(BacklogItemStatus.InSprint, fixture.Backlog.Status);
    }

    [Fact]
    public async Task FailResubmitPassPreservesBothQaHistoryEntries()
    {
        using var fixture = await QaFixture.CreateAsync();
        var task = await fixture.AddTaskAsync(TaskItemStatus.InReview);
        Assert.True((await fixture.Tasks.ReviewAsync(
            fixture.Review(task, QaReviewResult.Fail, "Bug still exists."))).Succeeded);
        Assert.True((await fixture.Tasks.TransitionStatusAsync(new TaskStatusTransitionRequest
        {
            ProjectId = fixture.Project.Id,
            SprintId = fixture.Sprint.Id,
            TaskId = task.Id,
            CurrentStatus = TaskItemStatus.InProgress,
            TargetStatus = TaskItemStatus.InReview,
            ActorUserId = "developer",
            Actor = TaskTransitionActor.Developer
        })).Succeeded);
        Assert.True((await fixture.Tasks.ReviewAsync(
            fixture.Review(task, QaReviewResult.Pass, "Retest passed."))).Succeeded);

        fixture.Context.ChangeTracker.Clear();
        var reviews = await fixture.Context.QaReviews.OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(2, reviews.Count);
        Assert.Equal(QaReviewResult.Fail, reviews[0].Result);
        Assert.Equal("Bug still exists.", reviews[0].Notes);
        Assert.Equal(QaReviewResult.Pass, reviews[1].Result);
        Assert.Equal(TaskItemStatus.Done, (await fixture.Context.TaskItems.SingleAsync()).Status);
        var details = await fixture.Tasks.GetByIdAsync(
            fixture.Project.Id, fixture.Sprint.Id, task.Id);
        Assert.NotNull(details);
        Assert.Equal(2, details.QaReviews.Count);
        Assert.Equal(QaReviewResult.Pass, details.QaReviews[0].Result);
        Assert.All(details.QaReviews, review => Assert.Equal("QA User", review.ReviewerName));
    }

    [Theory]
    [InlineData(TaskItemStatus.ToDo)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.InReview)]
    public async Task SprintCompletionRejectsAnyUnfinishedTask(TaskItemStatus status)
    {
        using var fixture = await QaFixture.CreateAsync();
        await fixture.AddTaskAsync(TaskItemStatus.Done);
        await fixture.AddTaskAsync(status);

        var result = await fixture.Sprints.CompleteAsync(
            fixture.Project.Id, fixture.Sprint.Id, "manager");

        Assert.False(result.Succeeded);
        Assert.Equal("All sprint tasks must be Done before completing the sprint.", result.Error);
        Assert.Equal(SprintStatus.InProgress, fixture.Sprint.Status);
    }

    [Fact]
    public async Task SprintCompletionSucceedsWhenAllTasksAreDone()
    {
        using var fixture = await QaFixture.CreateAsync();
        await fixture.AddTaskAsync(TaskItemStatus.Done);
        await fixture.AddTaskAsync(TaskItemStatus.Done);

        var result = await fixture.Sprints.CompleteAsync(
            fixture.Project.Id, fixture.Sprint.Id, "manager");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(SprintStatus.Completed, fixture.Sprint.Status);
    }

    [Fact]
    public async Task EmptySprintRetainsExistingCompletionBehavior()
    {
        using var fixture = await QaFixture.CreateAsync();

        var result = await fixture.Sprints.CompleteAsync(
            fixture.Project.Id, fixture.Sprint.Id, "manager");

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(SprintStatus.Completed, fixture.Sprint.Status);
    }

    [Fact]
    public async Task QaBoardShowsActionsOnlyForAuthorizedQaAndInReviewTasks()
    {
        using var fixture = await QaFixture.CreateAsync();
        await fixture.AddTaskAsync(TaskItemStatus.InReview);
        await fixture.AddTaskAsync(TaskItemStatus.InProgress);

        var qaBoard = Assert.IsType<TaskBoardViewModel>(Assert.IsType<ViewResult>(
            await fixture.Controller("qa", SystemRoles.QaTester)
                .Board(fixture.Project.Id, fixture.Sprint.Id, default)).Model);
        Assert.True(qaBoard.Tasks.Single(item => item.Status == TaskItemStatus.InReview).CanQaReview);
        Assert.False(qaBoard.Tasks.Single(item => item.Status == TaskItemStatus.InProgress).CanQaReview);

        var managerBoard = Assert.IsType<TaskBoardViewModel>(Assert.IsType<ViewResult>(
            await fixture.Controller("manager", SystemRoles.ProjectManager)
                .Board(fixture.Project.Id, fixture.Sprint.Id, default)).Model);
        Assert.All(managerBoard.Tasks, item => Assert.False(item.CanQaReview));
    }

    private sealed class QaFixture : IDisposable
    {
        public ApplicationDbContext Context { get; }
        public TaskService Tasks { get; }
        public FakeQaEvidenceStorage Storage { get; } = new();
        public SprintService Sprints { get; }
        public Project Project { get; } = new()
            { Id = 1, Name = "Scrum", Methodology = ProjectMethodology.Scrum, Status = ProjectStatus.Active };
        public Project OtherProject { get; } = new()
            { Id = 2, Name = "Other", Methodology = ProjectMethodology.Scrum, Status = ProjectStatus.Active };
        public Sprint Sprint { get; }
        public Sprint OtherSprint { get; }
        public BacklogItem Backlog { get; }
        public SprintBacklogItem Link { get; }
        private SprintBacklogItem ForeignLink { get; }

        private QaFixture()
        {
            Context = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            var clock = new TestClock();
            Tasks = new TaskService(Context, new TaskAssignmentService(Context), clock,
                qaEvidenceStorage: Storage);
            Sprints = new SprintService(Context, clock);
            Sprint = new Sprint
                { Id = 11, Project = Project, Name = "Sprint", Status = SprintStatus.InProgress };
            OtherSprint = new Sprint
                { Id = 12, Project = Project, Name = "Other Sprint", Status = SprintStatus.InProgress };
            Backlog = new BacklogItem
            {
                Id = 201,
                Project = Project,
                Title = "Story",
                Description = "Story",
                Status = BacklogItemStatus.InSprint
            };
            Link = new SprintBacklogItem
                { Id = 101, Sprint = Sprint, BacklogItem = Backlog, AddedByUserId = "manager" };
            ForeignLink = new SprintBacklogItem
            {
                Id = 102,
                Sprint = new Sprint
                    { Id = 21, Project = OtherProject, Name = "Foreign", Status = SprintStatus.InProgress },
                BacklogItem = new BacklogItem
                {
                    Id = 202,
                    Project = OtherProject,
                    Title = "Foreign",
                    Description = "Foreign",
                    Status = BacklogItemStatus.InSprint
                },
                AddedByUserId = "other-manager"
            };
        }

        public static async Task<QaFixture> CreateAsync()
        {
            var fixture = new QaFixture();
            fixture.Context.AddRange(fixture.Link, fixture.ForeignLink, fixture.OtherSprint);
            foreach (var (id, name) in new[]
            {
                ("admin", "Admin"), ("manager", "Manager"), ("scrum", "Scrum Master"),
                ("developer", "Developer"), ("qa", "QA User"),
                ("qa-nonmember", "QA Nonmember"), ("qa-wrong-role", "Wrong QA"),
                ("other-manager", "Other Manager")
            })
            {
                fixture.Context.Users.Add(new ApplicationUser { Id = id, UserName = id, FullName = name });
            }
            fixture.Context.ProjectMembers.AddRange(
                new ProjectMember { Project = fixture.Project, UserId = "manager", Role = ProjectMemberRole.ProjectManager },
                new ProjectMember { Project = fixture.Project, UserId = "scrum", Role = ProjectMemberRole.ScrumMaster },
                new ProjectMember { Project = fixture.Project, UserId = "developer", Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = fixture.Project, UserId = "qa", Role = ProjectMemberRole.QaTester },
                new ProjectMember { Project = fixture.Project, UserId = "qa-wrong-role", Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = fixture.OtherProject, UserId = "other-manager", Role = ProjectMemberRole.ProjectManager });
            await fixture.Context.SaveChangesAsync();
            return fixture;
        }

        public async Task<TaskItem> AddTaskAsync(
            TaskItemStatus status,
            SprintBacklogItem? link = null)
        {
            var task = new TaskItem
            {
                SprintBacklogItemId = (link ?? Link).Id,
                Title = $"Task {Guid.NewGuid():N}",
                Description = "Description",
                Status = status,
                AssignedUserId = "developer",
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
                Status = TaskItemStatus.InReview,
                AssignedUserId = "other-manager",
                CreatedByUserId = "other-manager"
            };
            Context.Add(task);
            await Context.SaveChangesAsync();
            return task;
        }

        public QaReviewTaskRequest Review(
            TaskItem task,
            QaReviewResult result,
            string? notes = null) => new()
        {
            ProjectId = Project.Id,
            SprintId = Sprint.Id,
            TaskId = task.Id,
            ReviewerUserId = "qa",
            Result = result,
            Notes = notes
        };

        public QaReviewTaskViewModel Model(
            TaskItem task,
            QaReviewResult result,
            string? notes = null) => new()
        {
            ProjectId = Project.Id,
            SprintId = Sprint.Id,
            TaskId = task.Id,
            Result = result,
            Notes = notes
        };

        public TasksController Controller(string userId, params string[] globalRoles)
        {
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
            claims.AddRange(globalRoles.Select(role => new Claim(ClaimTypes.Role, role)));
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
            };
            return new TasksController(
                Tasks,
                new ProjectService(Context, new TestClock()),
                Sprints,
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
        public static readonly DateTime Now = new(2026, 9, 7, 10, 30, 0, DateTimeKind.Utc);
        public DateTime UtcNow => Now;
    }

    private static QaReviewTaskRequest WithEvidence(QaReviewTaskRequest request, QaEvidenceUpload evidence) =>
        new()
        {
            ProjectId = request.ProjectId, SprintId = request.SprintId, TaskId = request.TaskId,
            ReviewerUserId = request.ReviewerUserId, Result = request.Result, Notes = request.Notes,
            EvidenceFiles = [evidence]
        };

    public sealed class FakeQaEvidenceStorage : IQaEvidenceStorage
    {
        public Dictionary<string, byte[]> Files { get; } = new();
        public async Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default)
        {
            string key = $"qa-{Guid.NewGuid():N}{extension}";
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            Files.Add(key, buffer.ToArray());
            return key;
        }
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(Files.TryGetValue(storageKey, out var bytes) ? new MemoryStream(bytes) : null);
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
        {
            Files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }
}
