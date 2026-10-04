using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Planora.Application.Common.Security;
using Planora.Application.Abstractions.Tasks;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;
using Planora.Web.Controllers;

namespace Planora.IntegrationTests;

public sealed class QaEvidenceAccessTests
{
    [Theory]
    [InlineData("admin", "Admin", true)]
    [InlineData("manager", "Project Manager", true)]
    [InlineData("developer", "Developer", true)]
    [InlineData("qa", "QA Tester", true)]
    [InlineData("outsider", "Developer", false)]
    public async Task EvidenceDownloadIsRestrictedToAuthorizedProjectRoles(
        string userId, string role, bool allowed)
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var project = new Project { Id = 1, Name = "Scrum", Methodology = ProjectMethodology.Scrum };
        var sprint = new Sprint { Id = 1, Project = project, Name = "Sprint" };
        var backlog = new BacklogItem { Id = 1, Project = project, Title = "Story" };
        var link = new SprintBacklogItem { Id = 1, Sprint = sprint, BacklogItem = backlog };
        var task = new TaskItem { Id = 1, SprintBacklogItem = link, Title = "Task", AssignedUserId = "developer" };
        var review = new QaReview { Id = 1, TaskItem = task, QaUserId = "qa", Result = QaReviewResult.Pass };
        db.AddRange(project, sprint, backlog, link, task, review,
            new QaEvidence { Id = 1, QaReview = review, OriginalFileName = "result.txt", StorageKey = "safe.txt",
                Extension = ".txt", FileSizeBytes = 4, UploadedByUserId = "qa", UploadedAt = DateTime.UtcNow },
            new ProjectMember { ProjectId = 1, UserId = "manager", Role = ProjectMemberRole.ProjectManager },
            new ProjectMember { ProjectId = 1, UserId = "qa", Role = ProjectMemberRole.QaTester });
        await db.SaveChangesAsync();
        var storage = new FakeStorage();
        var controller = new QaEvidenceController(db, storage, NullLogger<QaEvidenceController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Role, role)], "test"))
                }
            }
        };

        var result = await controller.Download(1, CancellationToken.None);

        if (allowed)
        {
            var file = Assert.IsType<FileStreamResult>(result);
            Assert.Equal("text/plain", file.ContentType);
            Assert.Equal("result.txt", file.FileDownloadName);
            await file.FileStream.DisposeAsync();
        }
        else Assert.IsType<ForbidResult>(result);
        var delete = typeof(QaEvidenceController).GetMethod(nameof(QaEvidenceController.Delete))!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().Single();
        Assert.Equal(AuthorizationPolicies.AdminOnly, delete.Policy);
    }

    private sealed class FakeStorage : IQaEvidenceStorage
    {
        public Task<string> SaveAsync(Stream content, string extension, CancellationToken cancellationToken = default) =>
            Task.FromResult("safe" + extension);
        public Task<Stream?> OpenReadAsync(string storageKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<Stream?>(new MemoryStream([1, 2, 3, 4]));
        public Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
