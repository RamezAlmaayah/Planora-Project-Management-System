using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Security;
using Planora.Application.Common.VModel;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;
using Planora.Infrastructure.Projects;
using Planora.Infrastructure.Requirements;
using Planora.Infrastructure.Security;
using Planora.Infrastructure.VModel;
using Planora.Web.Controllers;
using Planora.Web.ViewModels.VModel;

namespace Planora.IntegrationTests;

public sealed class VModelArtifactTests
{
    [Fact]
    public async Task DesignIdentifiersAreGeneratedPerProjectAndImmutable()
    {
        await using var f = await Fixture.CreateAsync();
        Requirement local = await f.RequirementAsync();
        Requirement foreign = await f.RequirementAsync(f.ForeignProject, "FR-001");
        ArtifactOperationResult first = await f.Service.CreateDesignArtifactAsync(
            f.DesignRequest(f.Manager.Id, [local.Id]));
        ArtifactOperationResult second = await f.Service.CreateDesignArtifactAsync(
            f.DesignRequest(f.Manager.Id, [local.Id], "Second"));
        ArtifactOperationResult isolated = await f.Service.CreateDesignArtifactAsync(
            f.DesignRequest(f.Admin.Id, [foreign.Id], projectId: f.ForeignProject.Id));

        Assert.Equal("DES-001", first.Identifier);
        Assert.Equal("DES-002", second.Identifier);
        Assert.Equal("DES-001", isolated.Identifier);
        Assert.Null(typeof(CreateDesignArtifactRequest).GetProperty("Identifier"));
        Assert.Null(typeof(UpdateDesignArtifactRequest).GetProperty("Identifier"));
    }

    [Fact]
    public async Task ConcurrentDesignIdentifiersRemainUnique()
    {
        await using var f = await Fixture.CreateAsync();
        Requirement requirement = await f.RequirementAsync();
        using IServiceScope aScope = f.Scope();
        using IServiceScope bScope = f.Scope();
        var a = aScope.ServiceProvider.GetRequiredService<VModelArtifactService>();
        var b = bScope.ServiceProvider.GetRequiredService<VModelArtifactService>();
        ArtifactOperationResult[] results = await Task.WhenAll(
            a.CreateDesignArtifactAsync(f.DesignRequest(f.Manager.Id, [requirement.Id], "A")),
            b.CreateDesignArtifactAsync(f.DesignRequest(f.Manager.Id, [requirement.Id], "B")));

        Assert.All(results, x => Assert.True(x.Succeeded, x.Message));
        Assert.Equal(2, results.Select(x => x.Identifier).Distinct().Count());
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("manager", true)]
    [InlineData("developer", false)]
    [InlineData("qa", false)]
    [InlineData("scrum", false)]
    [InlineData("outsider", false)]
    public async Task DesignAuthorizationMatrix(string actor, bool allowed)
    {
        await using var f = await Fixture.CreateAsync();
        Requirement requirement = await f.RequirementAsync();
        ArtifactOperationResult result = await f.Service.CreateDesignArtifactAsync(
            f.DesignRequest(f.User(actor).Id, [requirement.Id]));
        Assert.Equal(allowed, result.Succeeded);
        if (!allowed) Assert.Equal(ArtifactOperationFailure.Forbidden, result.Failure);
    }

    [Theory]
    [InlineData(RequirementStatus.Draft)]
    [InlineData(RequirementStatus.UnderReview)]
    [InlineData(RequirementStatus.Rejected)]
    [InlineData(RequirementStatus.Deprecated)]
    public async Task DesignRejectsNonApprovedNewRequirementLinks(RequirementStatus status)
    {
        await using var f = await Fixture.CreateAsync();
        Requirement requirement = await f.RequirementAsync(status: status);
        ArtifactOperationResult result = await f.Service.CreateDesignArtifactAsync(
            f.DesignRequest(f.Manager.Id, [requirement.Id]));
        Assert.Equal(ArtifactOperationFailure.Validation, result.Failure);
    }

    [Fact]
    public async Task DesignRejectsMissingDuplicateCrossProjectAndForgedLinks()
    {
        await using var f = await Fixture.CreateAsync();
        Requirement local = await f.RequirementAsync();
        Requirement foreign = await f.RequirementAsync(f.ForeignProject, "FR-001");
        ArtifactOperationResult[] results =
        [
            await f.Service.CreateDesignArtifactAsync(f.DesignRequest(f.Manager.Id, [])),
            await f.Service.CreateDesignArtifactAsync(f.DesignRequest(f.Manager.Id, [local.Id, local.Id])),
            await f.Service.CreateDesignArtifactAsync(f.DesignRequest(f.Manager.Id, [foreign.Id])),
            await f.Service.CreateDesignArtifactAsync(f.DesignRequest(f.Manager.Id, [int.MaxValue]))
        ];
        Assert.All(results, x => Assert.Equal(ArtifactOperationFailure.Validation, x.Failure));
    }

    [Fact]
    public async Task DeprecatedHistoricalDesignLinkIsPreservedAndLastLinkCannotBeRemoved()
    {
        await using var f = await Fixture.CreateAsync();
        Requirement requirement = await f.RequirementAsync();
        DesignArtifact design = await f.DesignAsync(requirement);
        requirement.Status = RequirementStatus.Deprecated;
        await f.Context.SaveChangesAsync();
        ArtifactOperationResult keep = await f.Service.UpdateDesignArtifactAsync(new()
        {
            ProjectId = f.Project.Id, ArtifactId = design.Id, Title = "Updated",
            Description = design.Description, Type = design.Type,
            RequirementIds = [requirement.Id], ActorUserId = f.Manager.Id
        });
        ArtifactOperationResult remove = await f.Service.UpdateDesignArtifactAsync(new()
        {
            ProjectId = f.Project.Id, ArtifactId = design.Id, Title = design.Title,
            Description = design.Description, Type = design.Type,
            RequirementIds = [], ActorUserId = f.Manager.Id
        });
        Assert.True(keep.Succeeded);
        Assert.Equal(ArtifactOperationFailure.Validation, remove.Failure);
        Assert.Single(f.Context.DesignArtifactRequirements);
    }

    [Fact]
    public async Task ScrumAndArchivedProjectsRejectDesignMutation()
    {
        await using var f = await Fixture.CreateAsync();
        Requirement scrum = await f.RequirementAsync(f.ScrumProject, "FR-001");
        Requirement archived = await f.RequirementAsync(f.ArchivedProject, "FR-001");
        ArtifactOperationResult scrumResult = await f.Service.CreateDesignArtifactAsync(
            f.DesignRequest(f.Admin.Id, [scrum.Id], projectId: f.ScrumProject.Id));
        ArtifactOperationResult archivedResult = await f.Service.CreateDesignArtifactAsync(
            f.DesignRequest(f.Admin.Id, [archived.Id], projectId: f.ArchivedProject.Id));
        Assert.Equal(ArtifactOperationFailure.Forbidden, scrumResult.Failure);
        Assert.Equal(ArtifactOperationFailure.ReadOnly, archivedResult.Failure);
    }

    [Fact]
    public async Task DesignRejectsForgedProjectAndEnumAndKeepsIdentifier()
    {
        await using var f = await Fixture.CreateAsync();
        Requirement requirement = await f.RequirementAsync();
        DesignArtifact design = await f.DesignAsync(requirement);
        ArtifactOperationResult project = await f.Service.UpdateDesignArtifactAsync(new()
        {
            ProjectId = f.ForeignProject.Id, ArtifactId = design.Id, Title = design.Title,
            Description = design.Description, Type = design.Type,
            RequirementIds = [requirement.Id], ActorUserId = f.Admin.Id
        });
        ArtifactOperationResult type = await f.Service.UpdateDesignArtifactAsync(new()
        {
            ProjectId = f.Project.Id, ArtifactId = design.Id, Title = design.Title,
            Description = design.Description, Type = (DesignArtifactType)999,
            RequirementIds = [requirement.Id], ActorUserId = f.Manager.Id
        });
        Assert.Equal(ArtifactOperationFailure.NotFound, project.Failure);
        Assert.Equal(ArtifactOperationFailure.Validation, type.Failure);
        Assert.Equal("DES-001", design.Identifier);
    }

    [Fact]
    public async Task ImplementationIdentifiersAreGeneratedPerProjectAndImmutable()
    {
        await using var f = await Fixture.CreateAsync();
        Requirement requirement = await f.RequirementAsync();
        DesignArtifact design = await f.DesignAsync(requirement);
        ArtifactOperationResult first = await f.Service.CreateImplementationArtifactAsync(
            f.ImplementationRequest(f.Developer.Id, [design.Id]));
        ArtifactOperationResult second = await f.Service.CreateImplementationArtifactAsync(
            f.ImplementationRequest(f.Developer.Id, [design.Id], "Second"));
        Assert.Equal("IMP-001", first.Identifier);
        Assert.Equal("IMP-002", second.Identifier);
        Assert.Null(typeof(CreateImplementationArtifactRequest).GetProperty("Identifier"));
        Assert.Null(typeof(UpdateImplementationArtifactRequest).GetProperty("Identifier"));
        Assert.Null(typeof(CreateImplementationArtifactRequest).GetProperty("CreatedByUserId"));
    }

    [Fact]
    public async Task ImplementationIdentifierSequenceIsIsolatedPerProject()
    {
        await using var f = await Fixture.CreateAsync();
        DesignArtifact local = await f.DesignAsync(await f.RequirementAsync());
        Requirement foreignRequirement = await f.RequirementAsync(f.ForeignProject, "FR-001");
        DesignArtifact foreign = await f.DesignAsync(foreignRequirement, f.Admin.Id, f.ForeignProject.Id);
        ArtifactOperationResult localResult = await f.Service.CreateImplementationArtifactAsync(
            f.ImplementationRequest(f.Developer.Id, [local.Id]));
        ArtifactOperationResult foreignResult = await f.Service.CreateImplementationArtifactAsync(
            f.ImplementationRequest(f.Admin.Id, [foreign.Id], projectId: f.ForeignProject.Id));
        Assert.Equal("IMP-001", localResult.Identifier);
        Assert.Equal("IMP-001", foreignResult.Identifier);
    }

    [Fact]
    public async Task ConcurrentImplementationIdentifiersRemainUnique()
    {
        await using var f = await Fixture.CreateAsync();
        DesignArtifact design = await f.DesignAsync(await f.RequirementAsync());
        using IServiceScope aScope = f.Scope();
        using IServiceScope bScope = f.Scope();
        var a = aScope.ServiceProvider.GetRequiredService<VModelArtifactService>();
        var b = bScope.ServiceProvider.GetRequiredService<VModelArtifactService>();
        ArtifactOperationResult[] results = await Task.WhenAll(
            a.CreateImplementationArtifactAsync(
                f.ImplementationRequest(f.Developer.Id, [design.Id], "Concurrent A")),
            b.CreateImplementationArtifactAsync(
                f.ImplementationRequest(f.Developer.Id, [design.Id], "Concurrent B")));
        Assert.All(results, x => Assert.True(x.Succeeded, x.Message));
        Assert.Equal(2, results.Select(x => x.Identifier).Distinct().Count());
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("manager", true)]
    [InlineData("developer", true)]
    [InlineData("qa", false)]
    [InlineData("scrum", false)]
    [InlineData("outsider", false)]
    public async Task ImplementationAuthorizationMatrix(string actor, bool allowed)
    {
        await using var f = await Fixture.CreateAsync();
        DesignArtifact design = await f.DesignAsync(await f.RequirementAsync());
        ArtifactOperationResult result = await f.Service.CreateImplementationArtifactAsync(
            f.ImplementationRequest(f.User(actor).Id, [design.Id]));
        Assert.Equal(allowed, result.Succeeded);
        if (!allowed) Assert.Equal(ArtifactOperationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task DeveloperMayEditOwnButNotAnotherDevelopersImplementation()
    {
        await using var f = await Fixture.CreateAsync();
        DesignArtifact design = await f.DesignAsync(await f.RequirementAsync());
        ImplementationArtifact own = await f.ImplementationAsync(design, f.Developer.Id);
        ImplementationArtifact other = await f.ImplementationAsync(design, f.OtherDeveloper.Id, "Other");
        ArtifactOperationResult allowed = await f.Service.UpdateImplementationArtifactAsync(
            f.ImplementationUpdate(own, f.Developer.Id, [design.Id], "Updated"));
        ArtifactOperationResult denied = await f.Service.UpdateImplementationArtifactAsync(
            f.ImplementationUpdate(other, f.Developer.Id, [design.Id], "Forged"));
        Assert.True(allowed.Succeeded);
        Assert.Equal(ArtifactOperationFailure.Forbidden, denied.Failure);
    }

    [Fact]
    public async Task ImplementationRejectsMissingDuplicateCrossProjectAndForgedDesigns()
    {
        await using var f = await Fixture.CreateAsync();
        DesignArtifact local = await f.DesignAsync(await f.RequirementAsync());
        Requirement foreignRequirement = await f.RequirementAsync(f.ForeignProject, "FR-001");
        DesignArtifact foreign = await f.DesignAsync(foreignRequirement, f.Admin.Id, f.ForeignProject.Id);
        ArtifactOperationResult[] results =
        [
            await f.Service.CreateImplementationArtifactAsync(f.ImplementationRequest(f.Developer.Id, [])),
            await f.Service.CreateImplementationArtifactAsync(f.ImplementationRequest(f.Developer.Id, [local.Id, local.Id])),
            await f.Service.CreateImplementationArtifactAsync(f.ImplementationRequest(f.Developer.Id, [foreign.Id])),
            await f.Service.CreateImplementationArtifactAsync(f.ImplementationRequest(f.Developer.Id, [int.MaxValue]))
        ];
        Assert.All(results, x => Assert.Equal(ArtifactOperationFailure.Validation, x.Failure));
    }

    [Fact]
    public async Task RealTraceabilityChainIsDerivedAndManualTraceIsPreserved()
    {
        await using var f = await Fixture.CreateAsync();
        Requirement requirement = await f.RequirementAsync();
        DesignArtifact design = await f.DesignAsync(requirement);
        ImplementationArtifact implementation = await f.ImplementationAsync(design, f.Developer.Id);
        f.Context.RequirementTraces.Add(new RequirementTrace
        {
            RequirementId = requirement.Id, Stage = RequirementTraceStage.Design,
            ReferenceCode = "DES-DOC-01", CreatedByUserId = f.Manager.Id, CreatedAt = f.Clock.UtcNow
        });
        await f.Context.SaveChangesAsync();
        var requirementService = new RequirementService(f.Context, f.Clock);
        var details = await requirementService.GetByIdAsync(f.Project.Id, requirement.Id);
        var matrix = await requirementService.GetTraceabilityAsync(f.Project.Id);
        Assert.Equal(design.Identifier, Assert.Single(details!.DesignArtifacts).Identifier);
        Assert.Equal(implementation.Identifier, Assert.Single(details.ImplementationArtifacts).Identifier);
        Assert.Equal("DES-DOC-01", Assert.Single(details.Traces).ReferenceCode);
        Assert.Single(Assert.Single(matrix).DesignArtifacts);
        Assert.Single(f.Context.RequirementTraces);
    }

    [Fact]
    public async Task ArtifactMutationsCreateAuditEvents()
    {
        await using var f = await Fixture.CreateAsync();
        Requirement first = await f.RequirementAsync();
        Requirement second = await f.RequirementAsync(identifier: "FR-002");
        DesignArtifact design = await f.DesignAsync(first);
        await f.Service.UpdateDesignArtifactAsync(new()
        {
            ProjectId = f.Project.Id, ArtifactId = design.Id, Title = "Updated",
            Description = design.Description, Type = design.Type,
            RequirementIds = [second.Id], ActorUserId = f.Manager.Id
        });
        DesignArtifact secondDesign = await f.DesignAsync(second);
        ImplementationArtifact implementation = await f.ImplementationAsync(design, f.Developer.Id);
        await f.Service.UpdateImplementationArtifactAsync(
            f.ImplementationUpdate(implementation, f.Developer.Id, [secondDesign.Id]));
        string[] actions = f.Context.ActivityLogs.Select(x => x.Action).ToArray();
        foreach (string expected in new[] { "DesignArtifactCreated", "DesignArtifactEdited",
                     "DesignRequirementLinked", "DesignRequirementUnlinked",
                     "ImplementationArtifactCreated", "ImplementationArtifactEdited",
                     "ImplementationDesignLinked", "ImplementationDesignUnlinked" })
            Assert.Contains(expected, actions);
    }

    [Fact]
    public async Task ControllersAllowMemberViewsAndRejectNonMemberAndScrumAccess()
    {
        await using var f = await Fixture.CreateAsync();
        IActionResult developer = await f.DesignController(f.Developer)
            .Index(f.Project.Id, null, default);
        IActionResult qa = await f.ImplementationController(f.Qa)
            .Index(f.Project.Id, null, default);
        IActionResult outsider = await f.DesignController(f.Outsider)
            .Index(f.Project.Id, null, default);
        IActionResult scrum = await f.DesignController(f.Admin)
            .Index(f.ScrumProject.Id, null, default);
        Assert.IsType<ViewResult>(developer);
        Assert.IsType<ViewResult>(qa);
        Assert.IsType<ForbidResult>(outsider);
        Assert.IsType<NotFoundResult>(scrum);
    }

    [Fact]
    public void WebContractsPreserveAntiforgeryEncodingAndOverpostingProtection()
    {
        string root = RepositoryRoot();
        string views = string.Join('\n', Directory.GetFiles(
                Path.Combine(root, "src", "Planora.Web", "Views"), "*.cshtml",
                SearchOption.AllDirectories)
            .Where(x => x.Contains("DesignArtifacts") || x.Contains("ImplementationArtifacts"))
            .Select(File.ReadAllText));
        Assert.DoesNotContain("Html.Raw", views, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AntiForgeryToken", views);
        Assert.Null(typeof(Planora.Web.ViewModels.VModel.DesignArtifactFormViewModel)
            .GetProperty("CreatedByUserId"));
        Assert.Null(typeof(CreateImplementationArtifactRequest).GetProperty("Identifier"));
    }

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Planora.sln")))
            current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        public ApplicationDbContext Context { get; }
        public VModelArtifactService Service { get; }
        public TestClock Clock { get; } = new();
        public ApplicationUser Admin { get; }
        public ApplicationUser Manager { get; }
        public ApplicationUser ScrumMaster { get; }
        public ApplicationUser Developer { get; }
        public ApplicationUser OtherDeveloper { get; }
        public ApplicationUser Qa { get; }
        public ApplicationUser Outsider { get; }
        public Project Project { get; }
        public Project ForeignProject { get; }
        public Project ScrumProject { get; }
        public Project ArchivedProject { get; }

        private Fixture(ServiceProvider provider)
        {
            _provider = provider;
            _scope = provider.CreateScope();
            Context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Service = _scope.ServiceProvider.GetRequiredService<VModelArtifactService>();
            var users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (string role in SystemRoles.All)
                Assert.True(roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult().Succeeded);
            Admin = AddUser(users, "admin", SystemRoles.Admin);
            Manager = AddUser(users, "manager", SystemRoles.ProjectManager);
            ScrumMaster = AddUser(users, "scrum", SystemRoles.ScrumMaster);
            Developer = AddUser(users, "developer", SystemRoles.Developer);
            OtherDeveloper = AddUser(users, "developer2", SystemRoles.Developer);
            Qa = AddUser(users, "qa", SystemRoles.QaTester);
            Outsider = AddUser(users, "outsider", SystemRoles.Developer);
            Project = NewProject("V-Model", ProjectMethodology.VModel, ProjectStatus.Active);
            ForeignProject = NewProject("Foreign", ProjectMethodology.VModel, ProjectStatus.Active);
            ScrumProject = NewProject("Scrum", ProjectMethodology.Scrum, ProjectStatus.Active);
            ArchivedProject = NewProject("Archived", ProjectMethodology.VModel, ProjectStatus.Archived);
            Context.AddRange(Project, ForeignProject, ScrumProject, ArchivedProject);
            Context.SaveChanges();
            Context.AddRange(Member(Manager, ProjectMemberRole.ProjectManager),
                Member(ScrumMaster, ProjectMemberRole.ScrumMaster),
                Member(Developer, ProjectMemberRole.Developer),
                Member(OtherDeveloper, ProjectMemberRole.Developer),
                Member(Qa, ProjectMemberRole.QaTester));
            Context.SaveChanges();
        }

        public static Task<Fixture> CreateAsync()
        {
            var services = new ServiceCollection();
            var databaseRoot = new InMemoryDatabaseRoot();
            string databaseName = Guid.NewGuid().ToString();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(x =>
                x.UseInMemoryDatabase(databaseName, databaseRoot));
            services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton<IClock, TestClock>();
            services.AddScoped<VModelArtifactService>();
            return Task.FromResult(new Fixture(services.BuildServiceProvider()));
        }

        public IServiceScope Scope() => _provider.CreateScope();
        public async Task<Requirement> RequirementAsync(Project? project = null,
            string identifier = "FR-001", RequirementStatus status = RequirementStatus.Approved)
        {
            var item = new Requirement { Project = project ?? Project, Identifier = identifier,
                Title = $"Requirement {identifier}", Description = "Requirement description",
                Type = RequirementType.Functional, Priority = PriorityLevel.High, Status = status,
                CreatedByUserId = Manager.Id, CreatedAt = Clock.UtcNow };
            Context.Add(item); await Context.SaveChangesAsync(); return item;
        }
        public async Task<DesignArtifact> DesignAsync(Requirement requirement,
            string? actor = null, int? projectId = null)
        {
            ArtifactOperationResult result = await Service.CreateDesignArtifactAsync(
                DesignRequest(actor ?? Manager.Id, [requirement.Id], projectId: projectId));
            Assert.True(result.Succeeded, result.Message);
            return await Context.DesignArtifacts.SingleAsync(x => x.Id == result.ArtifactId);
        }
        public async Task<ImplementationArtifact> ImplementationAsync(DesignArtifact design,
            string actor, string title = "Authentication service")
        {
            ArtifactOperationResult result = await Service.CreateImplementationArtifactAsync(
                ImplementationRequest(actor, [design.Id], title));
            Assert.True(result.Succeeded, result.Message);
            return await Context.ImplementationArtifacts.SingleAsync(x => x.Id == result.ArtifactId);
        }
        public CreateDesignArtifactRequest DesignRequest(string actor, IReadOnlyList<int> ids,
            string title = "Login architecture", int? projectId = null) => new()
            { ProjectId = projectId ?? Project.Id, Title = title,
                Description = "Architecture description", Type = DesignArtifactType.ArchitectureDesign,
                RequirementIds = ids, ActorUserId = actor };
        public CreateImplementationArtifactRequest ImplementationRequest(string actor,
            IReadOnlyList<int> ids, string title = "Authentication service", int? projectId = null) => new()
            { ProjectId = projectId ?? Project.Id, Title = title, Description = "Implementation description",
                Type = ImplementationArtifactType.Service, SourceReference = "src/AuthService.cs",
                DesignArtifactIds = ids, ActorUserId = actor };
        public UpdateImplementationArtifactRequest ImplementationUpdate(
            ImplementationArtifact item, string actor, IReadOnlyList<int> ids, string? title = null) => new()
            { ProjectId = item.ProjectId, ArtifactId = item.Id, Title = title ?? item.Title,
                Description = item.Description, Type = item.Type, SourceReference = item.SourceReference,
                DesignArtifactIds = ids, ActorUserId = actor };
        public ApplicationUser User(string name) => name switch
        { "admin" => Admin, "manager" => Manager, "developer" => Developer,
            "qa" => Qa, "scrum" => ScrumMaster, _ => Outsider };
        public DesignArtifactsController DesignController(ApplicationUser user) =>
            SetUser(new DesignArtifactsController(Service,
                new ProjectService(Context, Clock), new ProjectAccessService(Context)), user);
        public ImplementationArtifactsController ImplementationController(ApplicationUser user) =>
            SetUser(new ImplementationArtifactsController(Service,
                new ProjectService(Context, Clock), new ProjectAccessService(Context)), user);
        private T SetUser<T>(T controller, ApplicationUser user) where T : Controller
        {
            var identity = new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, user.Id)], "Test");
            foreach (string role in Context.UserRoles.Where(x => x.UserId == user.Id)
                .Join(Context.Roles, x => x.RoleId, x => x.Id, (_, x) => x.Name!).ToList())
                identity.AddClaim(new System.Security.Claims.Claim(
                    System.Security.Claims.ClaimTypes.Role, role));
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                { User = new System.Security.Claims.ClaimsPrincipal(identity) }
            };
            return controller;
        }
        private ProjectMember Member(ApplicationUser user, ProjectMemberRole role) =>
            new() { Project = Project, UserId = user.Id, Role = role };
        private static Project NewProject(string name, ProjectMethodology methodology,
            ProjectStatus status) => new() { Name = name, Description = "Description",
                Objectives = "Objectives", Scope = "Scope", Methodology = methodology,
                Status = status, StartDate = DateTime.UtcNow, CreatedByUserId = "seed",
                CreatedAt = DateTime.UtcNow };
        private static ApplicationUser AddUser(UserManager<ApplicationUser> manager,
            string name, string role)
        {
            var user = new ApplicationUser { UserName = $"{name}@artifact.test",
                Email = $"{name}@artifact.test", FullName = name, EmailConfirmed = true };
            Assert.True(manager.CreateAsync(user).GetAwaiter().GetResult().Succeeded);
            Assert.True(manager.AddToRoleAsync(user, role).GetAwaiter().GetResult().Succeeded);
            return user;
        }
        public async ValueTask DisposeAsync() { _scope.Dispose(); await _provider.DisposeAsync(); }
    }

    public sealed class TestClock : IClock
    {
        public DateTime UtcNow => new(2026, 9, 8, 18, 0, 0, DateTimeKind.Utc);
    }
}
