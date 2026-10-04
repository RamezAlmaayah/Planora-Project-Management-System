using System.IO.Compression;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Tasks;
using Planora.Application.Common.Security;
using Planora.Application.Common.Tasks;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;
using Planora.Infrastructure.Tasks;

namespace Planora.IntegrationTests;

public sealed class TaskCollaborationTests
{
    [Fact]
    public async Task ProjectMemberCanAddAndViewComment()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.AddCommentAsync(fixture.AddComment("A useful note"));
        var details = await fixture.Tasks.GetByIdAsync(
            fixture.Project.Id, fixture.Sprint.Id, fixture.Task.Id);

        Assert.True(result.Succeeded);
        var comment = Assert.Single(details!.Comments);
        Assert.Equal(fixture.Developer.FullName, comment.AuthorName);
        Assert.Equal("A useful note", comment.Content);
        Assert.Equal(fixture.Clock.UtcNow, comment.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n")]
    public async Task EmptyCommentIsRejected(string content)
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.AddCommentAsync(fixture.AddComment(content));

        Assert.False(result.Succeeded);
        Assert.Equal(TaskCollaborationFailure.Validation, result.Failure);
        Assert.Empty(fixture.Context.TaskComments);
    }

    [Fact]
    public async Task CommentLongerThanMappedLimitIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.AddCommentAsync(
            fixture.AddComment(new string('a', 4001)));

        Assert.False(result.Succeeded);
        Assert.Equal(TaskCollaborationFailure.Validation, result.Failure);
    }

    [Fact]
    public async Task NonMemberCannotAddComment()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.AddCommentAsync(
            fixture.AddComment("Denied", fixture.Outsider.Id));

        Assert.Equal(TaskCollaborationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task CrossProjectTaskCannotReceiveComment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.AddComment("Denied").WithIds(
            fixture.Project.Id, fixture.Sprint.Id, fixture.ForeignTask.Id);

        var result = await fixture.Service.AddCommentAsync(request);

        Assert.Equal(TaskCollaborationFailure.NotFound, result.Failure);
    }

    [Fact]
    public async Task WrongSprintCannotReceiveComment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = fixture.AddComment("Denied");
        var request = new AddTaskCommentRequest
        {
            ProjectId = source.ProjectId,
            SprintId = fixture.OtherSprint.Id,
            TaskId = source.TaskId,
            AuthorUserId = source.AuthorUserId,
            Content = source.Content
        };

        var result = await fixture.Service.AddCommentAsync(request);

        Assert.Equal(TaskCollaborationFailure.NotFound, result.Failure);
    }

    [Fact]
    public async Task CommentOwnerCanDeleteOwnComment()
    {
        await using var fixture = await Fixture.CreateAsync();
        int commentId = await fixture.SeedCommentAsync(fixture.Developer.Id);

        var result = await fixture.Service.DeleteCommentAsync(
            fixture.DeleteComment(commentId, fixture.Developer.Id));

        Assert.True(result.Succeeded);
        Assert.Empty(fixture.Context.TaskComments);
    }

    [Fact]
    public async Task MemberCannotDeleteAnotherUsersComment()
    {
        await using var fixture = await Fixture.CreateAsync();
        int commentId = await fixture.SeedCommentAsync(fixture.Developer.Id);

        var result = await fixture.Service.DeleteCommentAsync(
            fixture.DeleteComment(commentId, fixture.OtherMember.Id));

        Assert.Equal(TaskCollaborationFailure.Forbidden, result.Failure);
        Assert.Single(fixture.Context.TaskComments);
    }

    [Fact]
    public async Task AdminCanDeleteAnotherUsersComment()
    {
        await using var fixture = await Fixture.CreateAsync();
        int commentId = await fixture.SeedCommentAsync(fixture.Developer.Id);

        var result = await fixture.Service.DeleteCommentAsync(
            fixture.DeleteComment(commentId, fixture.Admin.Id));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ForeignCommentIdIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var foreignComment = new TaskComment
        {
            TaskItemId = fixture.ForeignTask.Id,
            AuthorUserId = fixture.Developer.Id,
            Content = "Foreign",
            CreatedAt = fixture.Clock.UtcNow
        };
        fixture.Context.Add(foreignComment);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.DeleteCommentAsync(
            fixture.DeleteComment(foreignComment.Id, fixture.Admin.Id));

        Assert.Equal(TaskCollaborationFailure.NotFound, result.Failure);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchivedProjectAndCompletedSprintMakeCommentsReadOnly(bool archiveProject)
    {
        await using var fixture = await Fixture.CreateAsync();
        int commentId = await fixture.SeedCommentAsync(fixture.Developer.Id);
        if (archiveProject) fixture.Project.Status = ProjectStatus.Archived;
        else fixture.Sprint.Status = SprintStatus.Completed;
        await fixture.Context.SaveChangesAsync();

        var add = await fixture.Service.AddCommentAsync(fixture.AddComment("Blocked"));
        var delete = await fixture.Service.DeleteCommentAsync(
            fixture.DeleteComment(commentId, fixture.Developer.Id));

        Assert.Equal(TaskCollaborationFailure.ReadOnly, add.Failure);
        Assert.Equal(TaskCollaborationFailure.ReadOnly, delete.Failure);
        Assert.Single(fixture.Context.TaskComments);
    }

    [Fact]
    public async Task CommentHtmlIsStoredAsPlainTextAndViewDoesNotUseHtmlRaw()
    {
        await using var fixture = await Fixture.CreateAsync();
        const string payload = "<script>alert('xss')</script>";

        var result = await fixture.Service.AddCommentAsync(fixture.AddComment(payload));
        string saved = (await fixture.Context.TaskComments.SingleAsync()).Content;
        string view = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "Planora.Web", "Views", "Tasks", "Details.cshtml"));

        Assert.True(result.Succeeded);
        Assert.Equal(payload, saved);
        Assert.DoesNotContain("Html.Raw", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p>@line</p>", view, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthorizedProjectManagerCanUploadAllowedFile()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.UploadAttachmentAsync(
            fixture.UploadPdf(fixture.ProjectManager.Id, "specification.pdf"));

        Assert.True(result.Succeeded);
        var attachment = await fixture.Context.TaskAttachments.SingleAsync();
        Assert.Equal("specification.pdf", attachment.OriginalFileName);
        Assert.Equal(".pdf", attachment.Extension);
        Assert.NotEqual("specification.pdf", attachment.StorageKey);
        Assert.True(fixture.Storage.Contains(attachment.StorageKey));
        Assert.Contains(fixture.Context.ActivityLogs,
            log => log.Action == "TaskAttachmentUploaded" &&
                   log.ResourceId == fixture.Task.Id.ToString());
    }

    [Fact]
    public async Task EmptyAttachmentIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.UploadPdf(fixture.ProjectManager.Id, "empty.pdf", []);

        var result = await fixture.Service.UploadAttachmentAsync(request);

        Assert.Equal(TaskCollaborationFailure.Validation, result.Failure);
        Assert.Empty(fixture.Context.TaskAttachments);
    }

    [Fact]
    public async Task OversizedAttachmentIsRejectedBeforeStorage()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = new UploadTaskAttachmentRequest
        {
            ProjectId = fixture.Project.Id,
            SprintId = fixture.Sprint.Id,
            TaskId = fixture.Task.Id,
            UploadedByUserId = fixture.ProjectManager.Id,
            OriginalFileName = "large.pdf",
            ContentType = "application/pdf",
            FileSizeBytes = TaskAttachmentOptions.DefaultMaximumFileSizeBytes + 1,
            Content = new MemoryStream("%PDF-"u8.ToArray())
        };

        var result = await fixture.Service.UploadAttachmentAsync(request);

        Assert.Equal(TaskCollaborationFailure.Validation, result.Failure);
        Assert.Empty(fixture.Storage.Keys);
    }

    [Theory]
    [InlineData("payload.exe")]
    [InlineData("library.dll")]
    [InlineData("script.js")]
    [InlineData("view.cshtml")]
    [InlineData("page.html")]
    [InlineData("shell.ps1")]
    [InlineData("command.bat")]
    public async Task DisallowedAndExecutableExtensionsAreRejected(string fileName)
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.UploadAttachmentAsync(
            fixture.UploadPdf(fixture.ProjectManager.Id, fileName));

        Assert.Equal(TaskCollaborationFailure.Validation, result.Failure);
        Assert.Empty(fixture.Storage.Keys);
    }

    [Fact]
    public async Task MismatchedContentTypeAndSignatureAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var wrongMime = fixture.UploadPdf(
            fixture.ProjectManager.Id, "spec.pdf", "%PDF-valid"u8.ToArray(), "text/html");
        var wrongSignature = fixture.UploadPdf(
            fixture.ProjectManager.Id, "spec.pdf", "not a pdf"u8.ToArray());

        var mimeResult = await fixture.Service.UploadAttachmentAsync(wrongMime);
        var signatureResult = await fixture.Service.UploadAttachmentAsync(wrongSignature);

        Assert.Equal(TaskCollaborationFailure.Validation, mimeResult.Failure);
        Assert.Equal(TaskCollaborationFailure.Validation, signatureResult.Failure);
    }

    [Fact]
    public async Task SafeZipPackageCanBeUploaded()
    {
        await using var fixture = await Fixture.CreateAsync();
        byte[] archive = CreateZip(("docs/readme.txt", "Planora attachment"));

        var result = await fixture.Service.UploadAttachmentAsync(
            fixture.UploadArchive(fixture.ProjectManager.Id, "documents.zip", archive));

        Assert.True(result.Succeeded);
        Assert.Single(fixture.Storage.Keys);
    }

    [Theory]
    [InlineData("payload.exe")]
    [InlineData("../outside.txt")]
    public async Task UnsafeZipEntriesAreRejected(string entryName)
    {
        await using var fixture = await Fixture.CreateAsync();
        byte[] archive = CreateZip((entryName, "unsafe"));

        var result = await fixture.Service.UploadAttachmentAsync(
            fixture.UploadArchive(fixture.ProjectManager.Id, "documents.zip", archive));

        Assert.Equal(TaskCollaborationFailure.Validation, result.Failure);
        Assert.Empty(fixture.Storage.Keys);
        Assert.Empty(fixture.Context.TaskAttachments);
    }

    [Theory]
    [InlineData("outsider")]
    [InlineData("developer")]
    [InlineData("qa")]
    public async Task UnauthorizedRolesCannotUpload(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();
        string userId = actor switch
        {
            "outsider" => fixture.Outsider.Id,
            "qa" => fixture.QaTester.Id,
            _ => fixture.Developer.Id
        };

        var result = await fixture.Service.UploadAttachmentAsync(
            fixture.UploadPdf(userId, "spec.pdf"));

        Assert.Equal(TaskCollaborationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task GlobalManagerWithWrongProjectRoleCannotUpload()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddGlobalRoleAsync(
            fixture.OtherMember, SystemRoles.ProjectManager);

        var result = await fixture.Service.UploadAttachmentAsync(
            fixture.UploadPdf(fixture.OtherMember.Id, "spec.pdf"));

        Assert.Equal(TaskCollaborationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task CrossProjectAndWrongSprintUploadsAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var original = fixture.UploadPdf(fixture.ProjectManager.Id, "spec.pdf");
        var crossProject = fixture.CopyUpload(original,
            fixture.Project.Id, fixture.Sprint.Id, fixture.ForeignTask.Id);
        var wrongSprint = fixture.CopyUpload(original,
            fixture.Project.Id, fixture.OtherSprint.Id, fixture.Task.Id);

        var first = await fixture.Service.UploadAttachmentAsync(crossProject);
        var second = await fixture.Service.UploadAttachmentAsync(wrongSprint);

        Assert.Equal(TaskCollaborationFailure.NotFound, first.Failure);
        Assert.Equal(TaskCollaborationFailure.NotFound, second.Failure);
    }

    [Fact]
    public async Task AuthorizedMemberCanDownloadButNonMemberCannot()
    {
        await using var fixture = await Fixture.CreateAsync();
        int attachmentId = await fixture.SeedAttachmentAsync(fixture.ProjectManager.Id);

        var allowed = await fixture.Service.DownloadAttachmentAsync(
            fixture.Download(attachmentId, fixture.Developer.Id));
        var denied = await fixture.Service.DownloadAttachmentAsync(
            fixture.Download(attachmentId, fixture.Outsider.Id));

        Assert.True(allowed.Succeeded);
        Assert.Equal("application/pdf", allowed.ContentType);
        Assert.Equal(TaskCollaborationFailure.Forbidden, denied.Failure);
        await allowed.Content!.DisposeAsync();
    }

    [Fact]
    public async Task ForeignAttachmentIdIsRejectedForDownloadAndDelete()
    {
        await using var fixture = await Fixture.CreateAsync();
        int foreignId = await fixture.SeedAttachmentAsync(
            fixture.ProjectManager.Id, fixture.ForeignTask.Id);

        var download = await fixture.Service.DownloadAttachmentAsync(
            fixture.Download(foreignId, fixture.Admin.Id));
        var delete = await fixture.Service.DeleteAttachmentAsync(
            fixture.DeleteAttachment(foreignId, fixture.Admin.Id));

        Assert.Equal(TaskCollaborationFailure.NotFound, download.Failure);
        Assert.Equal(TaskCollaborationFailure.NotFound, delete.Failure);
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("project-manager", true)]
    [InlineData("scrum-master-owner", true)]
    [InlineData("scrum-master-other", false)]
    [InlineData("developer", false)]
    public async Task AttachmentDeletePermissionsAreEnforced(string actor, bool succeeds)
    {
        await using var fixture = await Fixture.CreateAsync();
        string uploaderId = actor == "scrum-master-owner"
            ? fixture.ScrumMaster.Id
            : fixture.ProjectManager.Id;
        int attachmentId = await fixture.SeedAttachmentAsync(uploaderId);
        string actorId = actor switch
        {
            "admin" => fixture.Admin.Id,
            "project-manager" => fixture.ProjectManager.Id,
            "scrum-master-owner" or "scrum-master-other" => fixture.ScrumMaster.Id,
            _ => fixture.Developer.Id
        };

        var result = await fixture.Service.DeleteAttachmentAsync(
            fixture.DeleteAttachment(attachmentId, actorId));

        Assert.Equal(succeeds, result.Succeeded);
        Assert.Equal(!succeeds, await fixture.Context.TaskAttachments.AnyAsync());
        if (succeeds)
            Assert.Contains(fixture.Context.ActivityLogs,
                log => log.Action == "TaskAttachmentDeleted");
    }

    [Fact]
    public async Task ClientFileNameCannotControlPhysicalStorageOrTraverseDirectories()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.UploadAttachmentAsync(
            fixture.UploadPdf(fixture.ProjectManager.Id, "../../outside/owned.pdf"));
        var attachment = await fixture.Context.TaskAttachments.SingleAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("owned.pdf", attachment.OriginalFileName);
        Assert.DoesNotContain("owned", attachment.StorageKey, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("..", attachment.StorageKey, StringComparison.Ordinal);
        Assert.DoesNotContain('/', attachment.StorageKey);
        Assert.DoesNotContain('\\', attachment.StorageKey);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ArchivedProjectAndCompletedSprintMakeAttachmentsReadOnly(bool archiveProject)
    {
        await using var fixture = await Fixture.CreateAsync();
        int attachmentId = await fixture.SeedAttachmentAsync(fixture.ProjectManager.Id);
        if (archiveProject) fixture.Project.Status = ProjectStatus.Archived;
        else fixture.Sprint.Status = SprintStatus.Completed;
        await fixture.Context.SaveChangesAsync();

        var upload = await fixture.Service.UploadAttachmentAsync(
            fixture.UploadPdf(fixture.ProjectManager.Id, "blocked.pdf"));
        var delete = await fixture.Service.DeleteAttachmentAsync(
            fixture.DeleteAttachment(attachmentId, fixture.ProjectManager.Id));
        var download = await fixture.Service.DownloadAttachmentAsync(
            fixture.Download(attachmentId, fixture.Developer.Id));

        Assert.Equal(TaskCollaborationFailure.ReadOnly, upload.Failure);
        Assert.Equal(TaskCollaborationFailure.ReadOnly, delete.Failure);
        Assert.True(download.Succeeded);
        await download.Content!.DisposeAsync();
    }

    private static string RepositoryRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));

    private static byte[] CreateZip(params (string Name, string Content)[] entries)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = archive.CreateEntry(name);
                using StreamWriter writer = new(entry.Open());
                writer.Write(content);
            }
        }
        return stream.ToArray();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly UserManager<ApplicationUser> _users;
        public ApplicationDbContext Context { get; }
        public TaskCollaborationService Service { get; }
        public TaskService Tasks { get; }
        public FakeStorage Storage { get; } = new();
        public TestClock Clock { get; } = new();
        public Project Project { get; }
        public Sprint Sprint { get; }
        public Sprint OtherSprint { get; }
        public TaskItem Task { get; }
        public TaskItem ForeignTask { get; }
        public ApplicationUser Developer { get; }
        public ApplicationUser OtherMember { get; }
        public ApplicationUser Outsider { get; }
        public ApplicationUser ProjectManager { get; }
        public ApplicationUser ScrumMaster { get; }
        public ApplicationUser QaTester { get; }
        public ApplicationUser Admin { get; }

        private Fixture(ServiceProvider provider)
        {
            _provider = provider;
            Context = provider.GetRequiredService<ApplicationDbContext>();
            var users = provider.GetRequiredService<UserManager<ApplicationUser>>();
            _users = users;
            var roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (string role in SystemRoles.All)
                Assert.True(roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult().Succeeded);

            Developer = CreateUser(users, "developer", SystemRoles.Developer);
            OtherMember = CreateUser(users, "other", SystemRoles.Developer);
            Outsider = CreateUser(users, "outsider", SystemRoles.Developer);
            ProjectManager = CreateUser(users, "manager", SystemRoles.ProjectManager);
            ScrumMaster = CreateUser(users, "scrum", SystemRoles.ScrumMaster);
            QaTester = CreateUser(users, "qa", SystemRoles.QaTester);
            Admin = CreateUser(users, "admin", SystemRoles.Admin);

            Project = new Project
            {
                Name = "Project",
                Methodology = ProjectMethodology.Scrum,
                Status = ProjectStatus.Active
            };
            Sprint = new Sprint
            {
                Project = Project,
                Name = "Sprint",
                Status = SprintStatus.InProgress
            };
            OtherSprint = new Sprint
            {
                Project = Project,
                Name = "Other Sprint",
                Status = SprintStatus.InProgress
            };
            var backlog = new BacklogItem
            {
                Project = Project,
                Title = "Backlog",
                Status = BacklogItemStatus.InSprint
            };
            Task = new TaskItem
            {
                SprintBacklogItem = new SprintBacklogItem
                {
                    Sprint = Sprint,
                    BacklogItem = backlog
                },
                Title = "Task",
                AssignedUserId = Developer.Id,
                CreatedByUserId = ProjectManager.Id
            };

            var foreignProject = new Project
            {
                Name = "Foreign",
                Methodology = ProjectMethodology.Scrum,
                Status = ProjectStatus.Active
            };
            var foreignSprint = new Sprint
            {
                Project = foreignProject,
                Name = "Foreign Sprint",
                Status = SprintStatus.InProgress
            };
            ForeignTask = new TaskItem
            {
                SprintBacklogItem = new SprintBacklogItem
                {
                    Sprint = foreignSprint,
                    BacklogItem = new BacklogItem
                    {
                        Project = foreignProject,
                        Title = "Foreign backlog",
                        Status = BacklogItemStatus.InSprint
                    }
                },
                Title = "Foreign task",
                CreatedByUserId = ProjectManager.Id
            };
            Context.AddRange(
                Task,
                ForeignTask,
                OtherSprint,
                new ProjectMember { Project = Project, UserId = Developer.Id, Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = Project, UserId = OtherMember.Id, Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = Project, UserId = ProjectManager.Id, Role = ProjectMemberRole.ProjectManager },
                new ProjectMember { Project = Project, UserId = ScrumMaster.Id, Role = ProjectMemberRole.ScrumMaster },
                new ProjectMember { Project = Project, UserId = QaTester.Id, Role = ProjectMemberRole.QaTester });
            Context.SaveChanges();

            Service = new TaskCollaborationService(
                Context,
                Storage,
                Clock,
                Options.Create(new TaskAttachmentOptions()),
                NullLogger<TaskCollaborationService>.Instance);
            Tasks = new TaskService(Context, new TaskAssignmentService(Context), Clock);
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

        public AddTaskCommentRequest AddComment(string content, string? userId = null) => new()
        {
            ProjectId = Project.Id,
            SprintId = Sprint.Id,
            TaskId = Task.Id,
            AuthorUserId = userId ?? Developer.Id,
            Content = content
        };

        public DeleteTaskCommentRequest DeleteComment(int commentId, string userId) => new()
        {
            ProjectId = Project.Id,
            SprintId = Sprint.Id,
            TaskId = Task.Id,
            CommentId = commentId,
            ActorUserId = userId
        };

        public UploadTaskAttachmentRequest UploadPdf(
            string userId,
            string fileName,
            byte[]? content = null,
            string contentType = "application/pdf")
        {
            content ??= "%PDF-1.7\nPlanora"u8.ToArray();
            return new UploadTaskAttachmentRequest
            {
                ProjectId = Project.Id,
                SprintId = Sprint.Id,
                TaskId = Task.Id,
                UploadedByUserId = userId,
                OriginalFileName = fileName,
                ContentType = contentType,
                FileSizeBytes = content.Length,
                Content = new MemoryStream(content, writable: false)
            };
        }

        public UploadTaskAttachmentRequest UploadArchive(
            string userId,
            string fileName,
            byte[] content) => new()
            {
                ProjectId = Project.Id,
                SprintId = Sprint.Id,
                TaskId = Task.Id,
                UploadedByUserId = userId,
                OriginalFileName = fileName,
                ContentType = "application/zip",
                FileSizeBytes = content.Length,
                Content = new MemoryStream(content, writable: false)
            };

        public UploadTaskAttachmentRequest CopyUpload(
            UploadTaskAttachmentRequest source,
            int projectId,
            int sprintId,
            int taskId)
        {
            byte[] bytes = "%PDF-1.7\nPlanora"u8.ToArray();
            return new UploadTaskAttachmentRequest
            {
                ProjectId = projectId,
                SprintId = sprintId,
                TaskId = taskId,
                UploadedByUserId = source.UploadedByUserId,
                OriginalFileName = source.OriginalFileName,
                ContentType = source.ContentType,
                FileSizeBytes = bytes.Length,
                Content = new MemoryStream(bytes, writable: false)
            };
        }

        public DownloadTaskAttachmentRequest Download(int attachmentId, string userId) => new()
        {
            ProjectId = Project.Id,
            SprintId = Sprint.Id,
            TaskId = Task.Id,
            AttachmentId = attachmentId,
            ActorUserId = userId
        };

        public DeleteTaskAttachmentRequest DeleteAttachment(int attachmentId, string userId) => new()
        {
            ProjectId = Project.Id,
            SprintId = Sprint.Id,
            TaskId = Task.Id,
            AttachmentId = attachmentId,
            ActorUserId = userId
        };

        public async Task<int> SeedCommentAsync(string authorId)
        {
            var comment = new TaskComment
            {
                TaskItemId = Task.Id,
                AuthorUserId = authorId,
                Content = "Comment",
                CreatedAt = Clock.UtcNow
            };
            Context.Add(comment);
            await Context.SaveChangesAsync();
            return comment.Id;
        }

        public async Task AddGlobalRoleAsync(
            ApplicationUser user,
            string role)
        {
            Assert.True((await _users.AddToRoleAsync(user, role)).Succeeded);
        }

        public async Task<int> SeedAttachmentAsync(string uploaderId, int? taskId = null)
        {
            byte[] content = "%PDF-1.7\nPlanora"u8.ToArray();
            string key = await Storage.SaveAsync(
                new MemoryStream(content, writable: false), ".pdf");
            var attachment = new TaskAttachment
            {
                TaskItemId = taskId ?? Task.Id,
                OriginalFileName = "file.pdf",
                StorageKey = key,
                FileSizeBytes = content.Length,
                Extension = ".pdf",
                UploadedByUserId = uploaderId,
                UploadedAt = Clock.UtcNow
            };
            Context.Add(attachment);
            await Context.SaveChangesAsync();
            return attachment.Id;
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

        public async ValueTask DisposeAsync()
        {
            await _provider.DisposeAsync();
        }
    }

    private sealed class FakeStorage : ITaskAttachmentStorage
    {
        private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);
        public IReadOnlyCollection<string> Keys => _files.Keys;
        public bool Contains(string key) => _files.ContainsKey(key);

        public async Task<string> SaveAsync(
            Stream content,
            string extension,
            CancellationToken cancellationToken = default)
        {
            string key = $"{Guid.NewGuid():N}{extension}";
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            _files.Add(key, buffer.ToArray());
            return key;
        }

        public Task<Stream?> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            Stream? stream = _files.TryGetValue(storageKey, out byte[]? content)
                ? new MemoryStream(content, writable: false)
                : null;
            return Task.FromResult(stream);
        }

        public Task DeleteAsync(
            string storageKey,
            CancellationToken cancellationToken = default)
        {
            _files.Remove(storageKey);
            return Task.CompletedTask;
        }

        public async Task RestoreAsync(
            string storageKey,
            Stream content,
            CancellationToken cancellationToken = default)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            _files.Add(storageKey, buffer.ToArray());
        }
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
    }
}

internal static class TaskCommentRequestTestExtensions
{
    public static AddTaskCommentRequest WithIds(
        this AddTaskCommentRequest source,
        int projectId,
        int sprintId,
        int taskId) => new()
        {
            ProjectId = projectId,
            SprintId = sprintId,
            TaskId = taskId,
            AuthorUserId = source.AuthorUserId,
            Content = source.Content
        };
}
