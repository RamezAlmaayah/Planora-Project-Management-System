using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Planora.Application.Common.Security;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Persistence;

namespace Planora.Infrastructure.Identity;

public static class DevelopmentDemoSeeder
{
    private const string PasswordKey = "DevelopmentDemoSeed:Password";
    private const string ScrumProjectName = "Planora Demo Scrum Project";
    private const string VModelProjectName = "Planora Demo V-Model Project";

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration,
        IHostEnvironment environment, CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment()) return;
        string? password = configuration[PasswordKey];
        if (string.IsNullOrWhiteSpace(password))
        {
            services.GetRequiredService<ILoggerFactory>().CreateLogger("DevelopmentDemoSeeder")
                .LogWarning("Development demo data was skipped because {PasswordKey} is not configured.", PasswordKey);
            return;
        }

        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        foreach (string role in SystemRoles.All)
            if (!await roles.RoleExistsAsync(role))
                Ensure(await roles.CreateAsync(new IdentityRole(role)), "role");

        var demo = new (string Email, string Name, string Role)[]
        {
            ("admin.demo@planora.local", "Demo Administrator", SystemRoles.Admin),
            ("pm.demo@planora.local", "Demo Project Manager", SystemRoles.ProjectManager),
            ("scrum.demo@planora.local", "Demo Scrum Master", SystemRoles.ScrumMaster),
            ("developer.demo@planora.local", "Demo Developer", SystemRoles.Developer),
            ("qa.demo@planora.local", "Demo QA Tester", SystemRoles.QaTester)
        };
        var demoUsers = new Dictionary<string, ApplicationUser>(StringComparer.Ordinal);
        foreach (var item in demo)
        {
            var user = await users.FindByEmailAsync(item.Email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = item.Email, Email = item.Email, EmailConfirmed = true,
                    FullName = item.Name, CreatedAt = DateTime.UtcNow
                };
                Ensure(await users.CreateAsync(user, password), "demo user");
            }
            if (!await users.IsInRoleAsync(user, item.Role))
                Ensure(await users.AddToRoleAsync(user, item.Role), "demo user role");
            demoUsers.Add(item.Role, user);
        }

        DateTime now = DateTime.UtcNow;
        var scrum = await GetProjectAsync(db, ScrumProjectName, ProjectMethodology.Scrum,
            demoUsers[SystemRoles.ProjectManager].Id, now, cancellationToken);
        var vmodel = await GetProjectAsync(db, VModelProjectName, ProjectMethodology.VModel,
            demoUsers[SystemRoles.ProjectManager].Id, now, cancellationToken);
        await EnsureMembershipsAsync(db, scrum, demoUsers, now, cancellationToken);
        await EnsureMembershipsAsync(db, vmodel, demoUsers, now, cancellationToken);
        await SeedScrumAsync(db, scrum, demoUsers, now, cancellationToken);
        await SeedVModelAsync(db, vmodel, demoUsers, now, cancellationToken);
    }

    private static async Task<Project> GetProjectAsync(ApplicationDbContext db, string name,
        ProjectMethodology methodology, string creator, DateTime now, CancellationToken ct)
    {
        var project = await db.Projects.SingleOrDefaultAsync(item => item.Name == name, ct);
        if (project is not null) return project;
        project = new Project
        {
            Name = name, Description = "Clearly identified Planora development demonstration data.",
            Objectives = "Demonstrate the Planora project workflow.", Scope = "Local demo only.",
            Methodology = methodology, Status = ProjectStatus.Active, StartDate = now.Date.AddDays(-14),
            CreatedByUserId = creator, CreatedAt = now
        };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);
        return project;
    }

    private static async Task EnsureMembershipsAsync(ApplicationDbContext db, Project project,
        IReadOnlyDictionary<string, ApplicationUser> users, DateTime now, CancellationToken ct)
    {
        var assignments = new[]
        {
            (SystemRoles.ProjectManager, ProjectMemberRole.ProjectManager),
            (SystemRoles.ScrumMaster, ProjectMemberRole.ScrumMaster),
            (SystemRoles.Developer, ProjectMemberRole.Developer),
            (SystemRoles.QaTester, ProjectMemberRole.QaTester)
        };
        foreach (var (globalRole, projectRole) in assignments)
        {
            string userId = users[globalRole].Id;
            if (!await db.ProjectMembers.AnyAsync(member => member.ProjectId == project.Id && member.UserId == userId, ct))
                db.ProjectMembers.Add(new ProjectMember
                { ProjectId = project.Id, UserId = userId, Role = projectRole, JoinedAt = now });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedScrumAsync(ApplicationDbContext db, Project project,
        IReadOnlyDictionary<string, ApplicationUser> users, DateTime now, CancellationToken ct)
    {
        var backlogItems = new Dictionary<BacklogItemStatus, BacklogItem>();
        foreach (var status in Enum.GetValues<BacklogItemStatus>())
        {
            string title = $"Demo backlog · {status}";
            var item = await db.BacklogItems.SingleOrDefaultAsync(row => row.ProjectId == project.Id && row.Title == title, ct);
            if (item is null)
            {
                item = new BacklogItem
                {
                    ProjectId = project.Id, Title = title,
                    Description = $"Representative {status} backlog work for the local Planora demonstration.",
                    Status = status, Priority = PriorityLevel.Medium,
                    CreatedByUserId = users[SystemRoles.ProjectManager].Id, CreatedAt = now
                };
                db.BacklogItems.Add(item);
                await db.SaveChangesAsync(ct);
            }
            backlogItems.Add(status, item);
        }

        var sprint = await db.Sprints.SingleOrDefaultAsync(row => row.ProjectId == project.Id && row.Name == "Demo active sprint", ct);
        if (sprint is null)
        {
            sprint = new Sprint
            {
                ProjectId = project.Id, Name = "Demo active sprint", Goal = "Complete the representative user workflow.",
                StartDate = now.Date.AddDays(-7), EndDate = now.Date.AddDays(7), Status = SprintStatus.InProgress,
                CreatedByUserId = users[SystemRoles.ScrumMaster].Id, CreatedAt = now
            };
            db.Sprints.Add(sprint);
            await db.SaveChangesAsync(ct);
        }

        var activeItem = backlogItems[BacklogItemStatus.InSprint];
        var link = await db.SprintBacklogItems.SingleOrDefaultAsync(row => row.SprintId == sprint.Id && row.BacklogItemId == activeItem.Id, ct);
        if (link is null)
        {
            link = new SprintBacklogItem
            { SprintId = sprint.Id, BacklogItemId = activeItem.Id, AddedByUserId = users[SystemRoles.ScrumMaster].Id, AddedAt = now };
            db.SprintBacklogItems.Add(link);
            await db.SaveChangesAsync(ct);
        }
        foreach (var status in Enum.GetValues<TaskItemStatus>())
        {
            string title = $"Demo task · {status}";
            if (!await db.TaskItems.AnyAsync(row => row.SprintBacklogItemId == link.Id && row.Title == title, ct))
                db.TaskItems.Add(new TaskItem
                {
                    SprintBacklogItemId = link.Id, Title = title,
                    Description = $"Representative task in the {status} state.", Priority = PriorityLevel.Medium,
                    Status = status, AssignedUserId = users[SystemRoles.Developer].Id,
                    CreatedByUserId = users[SystemRoles.ScrumMaster].Id, CreatedAt = now
                });
        }
        await db.SaveChangesAsync(ct);
        var reviewTask = await db.TaskItems.SingleAsync(row => row.SprintBacklogItemId == link.Id && row.Status == TaskItemStatus.InReview, ct);
        if (!await db.TaskComments.AnyAsync(row => row.TaskItemId == reviewTask.Id, ct))
        {
            db.TaskComments.Add(new TaskComment
            {
                TaskItemId = reviewTask.Id, AuthorUserId = users[SystemRoles.Developer].Id,
                Content = "Demo comment: implementation is ready for QA review.", CreatedAt = now
            });
        }
        if (!await db.Issues.AnyAsync(row => row.ProjectId == project.Id && row.Title == "Demo issue: validation feedback", ct))
            db.Issues.Add(new Issue
            {
                ProjectId = project.Id, TaskItemId = reviewTask.Id,
                Title = "Demo issue: validation feedback", Description = "Representative development issue for the demo.",
                Severity = IssueSeverity.Medium, Priority = PriorityLevel.Medium, Status = IssueStatus.Open,
                ReporterUserId = users[SystemRoles.QaTester].Id, AssignedUserId = users[SystemRoles.Developer].Id,
                CreatedAt = now
            });
        string developerId = users[SystemRoles.Developer].Id;
        if (!await db.Notifications.AnyAsync(row => row.ProjectId == project.Id && row.UserId == developerId && row.Title == "Demo notification", ct))
            db.Notifications.Add(new Notification
            {
                UserId = developerId, ProjectId = project.Id, Type = NotificationType.TaskSentToQa,
                Title = "Demo notification", Message = "A demonstration task is waiting for QA.",
                TargetUrl = $"/Tasks/Details?projectId={project.Id}&sprintId={sprint.Id}&id={reviewTask.Id}", CreatedAt = now
            });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedVModelAsync(ApplicationDbContext db, Project project,
        IReadOnlyDictionary<string, ApplicationUser> users, DateTime now, CancellationToken ct)
    {
        var requirement = await db.Requirements.SingleOrDefaultAsync(row => row.ProjectId == project.Id && row.Identifier == "DEMO-REQ-001", ct);
        if (requirement is null)
        {
            requirement = new Requirement
            {
                ProjectId = project.Id, Identifier = "DEMO-REQ-001", Title = "Demo traceable requirement",
                Description = "The demonstration system shall retain a traceable requirement lifecycle.",
                Type = RequirementType.Functional, Priority = PriorityLevel.High, Status = RequirementStatus.Approved,
                CreatedByUserId = users[SystemRoles.ProjectManager].Id, CreatedAt = now
            };
            db.Requirements.Add(requirement);
            await db.SaveChangesAsync(ct);
        }
        foreach (var (stage, code, description) in new[]
        {
            (RequirementTraceStage.Design, "DEMO-DESIGN-001", "Design representation for the demo requirement."),
            (RequirementTraceStage.Implementation, "DEMO-IMPL-001", "Implementation representation for the demo requirement."),
            (RequirementTraceStage.Verification, "DEMO-TEST-001", "Verification evidence for the demo requirement."),
            (RequirementTraceStage.Validation, "DEMO-VALIDATION-001", "Validation evidence for the demo requirement.")
        })
        {
            if (!await db.RequirementTraces.AnyAsync(row => row.RequirementId == requirement.Id && row.Stage == stage, ct))
                db.RequirementTraces.Add(new RequirementTrace
                {
                    RequirementId = requirement.Id, Stage = stage, ReferenceCode = code,
                    Description = description, CreatedByUserId = users[SystemRoles.ScrumMaster].Id, CreatedAt = now
                });
        }
        await db.SaveChangesAsync(ct);
    }

    private static void Ensure(IdentityResult result, string resource)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException($"Development demo {resource} could not be created: " +
                string.Join(", ", result.Errors.Select(error => error.Code)));
    }
}
