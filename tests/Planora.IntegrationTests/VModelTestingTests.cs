using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Projects;
using Planora.Application.Common.Requirements;
using Planora.Application.Common.Security;
using Planora.Application.Common.VModelTesting;
using Planora.Domain;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;
using Planora.Infrastructure.Projects;
using Planora.Infrastructure.Requirements;
using Planora.Infrastructure.Security;
using Planora.Infrastructure.VModel;
using Planora.Web.Controllers;

namespace Planora.IntegrationTests;

public sealed class VModelTestingTests
{
    [Fact]
    public async Task ProjectCreationInitializesCentralPhaseSequenceOnlyForVModel()
    {
        await using var f = await Fixture.CreateAsync();
        var projects = new ProjectService(f.Context, f.Clock);
        var v = await projects.CreateProjectAsync(NewProjectRequest("New V", ProjectMethodology.VModel, f.Admin.Id));
        var scrum = await projects.CreateProjectAsync(NewProjectRequest("New Scrum", ProjectMethodology.Scrum, f.Admin.Id));

        var phases = await f.Context.VModelPhases.Where(x => x.ProjectId == v.Id)
            .OrderBy(x => x.PhaseOrder).ToListAsync();
        Assert.Equal(VModelPhaseCatalog.Ordered, phases.Select(x => x.PhaseType));
        Assert.All(phases, x => Assert.Equal(VModelPhaseStatus.NotStarted, x.Status));
        Assert.Empty(f.Context.VModelPhases.Where(x => x.ProjectId == scrum.Id));
        Assert.Equal(VModelPhaseType.UnitTesting, VModelPhaseCatalog.ForTestLevel(VModelTestLevel.Unit));
        Assert.Equal(VModelPhaseType.AcceptanceValidation, VModelPhaseCatalog.ForTestLevel(VModelTestLevel.Acceptance));
    }

    [Fact]
    public async Task MissingPhaseInitializationIsIdempotent()
    {
        await using var f = await Fixture.CreateAsync(seedPhases: false);
        TestingOperationResult first = await f.Service.EnsurePhasesAsync(f.Project.Id, f.Manager.Id);
        TestingOperationResult second = await f.Service.EnsurePhasesAsync(f.Project.Id, f.Manager.Id);
        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Equal(8, await f.Context.VModelPhases.CountAsync(x => x.ProjectId == f.Project.Id));
    }

    [Fact]
    public async Task PhaseSequenceAndTerminalCompletedStateAreEnforced()
    {
        await using var f = await Fixture.CreateAsync();
        VModelPhase first = f.Phase(VModelPhaseType.Requirements);
        VModelPhase second = f.Phase(VModelPhaseType.SystemDesign);
        Assert.True((await f.Service.TransitionPhaseAsync(f.Transition(first, VModelPhaseStatus.NotStarted, VModelPhaseStatus.InProgress))).Succeeded);
        Assert.Equal(TestingOperationFailure.Conflict, (await f.Service.TransitionPhaseAsync(f.Transition(second, VModelPhaseStatus.NotStarted, VModelPhaseStatus.InProgress))).Failure);
        Assert.Equal(TestingOperationFailure.Validation, (await f.Service.TransitionPhaseAsync(f.Transition(first, VModelPhaseStatus.InProgress, VModelPhaseStatus.NotStarted))).Failure);
        Assert.True((await f.Service.TransitionPhaseAsync(f.Transition(first, VModelPhaseStatus.InProgress, VModelPhaseStatus.Completed))).Succeeded);
        Assert.True((await f.Service.TransitionPhaseAsync(f.Transition(second, VModelPhaseStatus.NotStarted, VModelPhaseStatus.InProgress))).Succeeded);
        Assert.Equal(TestingOperationFailure.Validation, (await f.Service.TransitionPhaseAsync(f.Transition(first, VModelPhaseStatus.Completed, VModelPhaseStatus.InProgress))).Failure);
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("manager", true)]
    [InlineData("qa", false)]
    [InlineData("developer", false)]
    [InlineData("scrum", false)]
    public async Task PhaseMutationAuthorizationIsExact(string actor, bool allowed)
    {
        await using var f = await Fixture.CreateAsync();
        TestingOperationResult result = await f.Service.TransitionPhaseAsync(
            f.Transition(f.Phase(VModelPhaseType.Requirements), VModelPhaseStatus.NotStarted,
                VModelPhaseStatus.InProgress, f.User(actor).Id));
        Assert.Equal(allowed, result.Succeeded);
        if (!allowed) Assert.Equal(TestingOperationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task ArchivedAndScrumProjectsRejectPhaseMutations()
    {
        await using var f = await Fixture.CreateAsync();
        VModelPhase archived = f.Phase(VModelPhaseType.Requirements, f.ArchivedProject.Id);
        Assert.Equal(TestingOperationFailure.ReadOnly,
            (await f.Service.TransitionPhaseAsync(f.Transition(archived, VModelPhaseStatus.NotStarted,
                VModelPhaseStatus.InProgress, f.Admin.Id, f.ArchivedProject.Id))).Failure);
        Assert.Equal(TestingOperationFailure.Forbidden,
            (await f.Service.EnsurePhasesAsync(f.ScrumProject.Id, f.Admin.Id)).Failure);
    }

    [Fact]
    public async Task TestCaseIdentifiersAreGeneratedPerProjectAndContractsAreServerControlled()
    {
        await using var f = await Fixture.CreateAsync();
        ImplementationArtifact local = await f.ImplementationAsync();
        ImplementationArtifact foreign = await f.ImplementationAsync(f.ForeignProject);
        TestingOperationResult first = await f.Service.CreateTestCaseAsync(f.TestRequest(f.Qa.Id, [local.Id]));
        TestingOperationResult second = await f.Service.CreateTestCaseAsync(f.TestRequest(f.Qa.Id, [local.Id], "Second"));
        TestingOperationResult isolated = await f.Service.CreateTestCaseAsync(f.TestRequest(f.Admin.Id, [foreign.Id], projectId: f.ForeignProject.Id));
        Assert.Equal("TC-001", first.Identifier);
        Assert.Equal("TC-002", second.Identifier);
        Assert.Equal("TC-001", isolated.Identifier);
        Assert.Null(typeof(CreateVModelTestCaseRequest).GetProperty("Identifier"));
        Assert.Null(typeof(CreateVModelTestCaseRequest).GetProperty("CreatedByUserId"));
        Assert.Null(typeof(ExecuteVModelTestCaseRequest).GetProperty("ExecutedByUserId"));
        Assert.Null(typeof(ValidateVModelPhaseRequest).GetProperty("ValidatedByUserId"));
    }

    [Fact]
    public async Task ConcurrentTestCaseIdentifiersRemainUnique()
    {
        await using var f = await Fixture.CreateAsync();
        ImplementationArtifact implementation = await f.ImplementationAsync();
        using IServiceScope aScope = f.Scope();
        using IServiceScope bScope = f.Scope();
        var a = aScope.ServiceProvider.GetRequiredService<VModelTestingService>();
        var b = bScope.ServiceProvider.GetRequiredService<VModelTestingService>();
        TestingOperationResult[] results = await Task.WhenAll(
            a.CreateTestCaseAsync(f.TestRequest(f.Qa.Id, [implementation.Id], "A")),
            b.CreateTestCaseAsync(f.TestRequest(f.Qa.Id, [implementation.Id], "B")));
        Assert.All(results, result => Assert.True(result.Succeeded, result.Message));
        Assert.Equal(2, results.Select(result => result.Identifier).Distinct().Count());
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("qa", true)]
    [InlineData("manager", false)]
    [InlineData("developer", false)]
    [InlineData("scrum", false)]
    [InlineData("outsider", false)]
    public async Task TestCaseMutationAuthorizationIsExact(string actor, bool allowed)
    {
        await using var f = await Fixture.CreateAsync();
        ImplementationArtifact implementation = await f.ImplementationAsync();
        TestingOperationResult result = await f.Service.CreateTestCaseAsync(
            f.TestRequest(f.User(actor).Id, [implementation.Id]));
        Assert.Equal(allowed, result.Succeeded);
        if (!allowed) Assert.Equal(TestingOperationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task TestCaseLinksRequireOneUniqueSameProjectImplementation()
    {
        await using var f = await Fixture.CreateAsync();
        ImplementationArtifact local = await f.ImplementationAsync();
        ImplementationArtifact foreign = await f.ImplementationAsync(f.ForeignProject);
        TestingOperationResult[] invalid =
        [
            await f.Service.CreateTestCaseAsync(f.TestRequest(f.Qa.Id, [])),
            await f.Service.CreateTestCaseAsync(f.TestRequest(f.Qa.Id, [local.Id, local.Id])),
            await f.Service.CreateTestCaseAsync(f.TestRequest(f.Qa.Id, [foreign.Id])),
            await f.Service.CreateTestCaseAsync(f.TestRequest(f.Qa.Id, [int.MaxValue]))
        ];
        Assert.All(invalid, result => Assert.Equal(TestingOperationFailure.Validation, result.Failure));
    }

    [Fact]
    public async Task EditPreservesIdentifierAndCannotRemoveFinalImplementationLink()
    {
        await using var f = await Fixture.CreateAsync();
        ImplementationArtifact implementation = await f.ImplementationAsync();
        VModelTestCase testCase = await f.TestCaseAsync(implementation);
        TestingOperationResult empty = await f.Service.UpdateTestCaseAsync(f.Update(testCase, []));
        TestingOperationResult valid = await f.Service.UpdateTestCaseAsync(f.Update(testCase, [implementation.Id], "Updated"));
        Assert.Equal(TestingOperationFailure.Validation, empty.Failure);
        Assert.True(valid.Succeeded);
        Assert.Equal("TC-001", (await f.Context.VModelTestCases.FindAsync(testCase.Id))!.Identifier);
    }

    [Fact]
    public async Task TestExecutionRequiresStartedMappedPhaseAndPreservesHistoryAndLatestResult()
    {
        await using var f = await Fixture.CreateAsync();
        VModelTestCase testCase = await f.TestCaseAsync(await f.ImplementationAsync());
        Assert.Equal(TestingOperationFailure.Conflict,
            (await f.Service.ExecuteAsync(f.Execution(testCase, VModelTestResult.Pass))).Failure);
        VModelPhase unit = f.Phase(VModelPhaseType.UnitTesting);
        unit.Status = VModelPhaseStatus.InProgress;
        await f.Context.SaveChangesAsync();
        TestingOperationResult pass = await f.Service.ExecuteAsync(f.Execution(testCase, VModelTestResult.Pass));
        TestingOperationResult fail = await f.Service.ExecuteAsync(f.Execution(testCase, VModelTestResult.Fail));
        Assert.True(pass.Succeeded);
        Assert.True(fail.Succeeded);
        Assert.Equal(2, await f.Context.VModelTestExecutions.CountAsync(x => x.VModelTestCaseId == testCase.Id));
        VModelTestCaseDetails details = (await f.Service.GetTestCaseAsync(f.Project.Id, testCase.Id))!;
        Assert.Equal(VModelTestResult.Fail, details.LatestResult);
        Assert.Equal(f.Qa.Id, (await f.Context.VModelTestExecutions.FindAsync(fail.ResourceId))!.ExecutedByUserId);
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("qa", true)]
    [InlineData("manager", false)]
    [InlineData("developer", false)]
    [InlineData("scrum", false)]
    public async Task TestExecutionAuthorizationIsExact(string actor, bool allowed)
    {
        await using var f = await Fixture.CreateAsync();
        VModelTestCase testCase = await f.TestCaseAsync(await f.ImplementationAsync());
        f.Phase(VModelPhaseType.UnitTesting).Status = VModelPhaseStatus.InProgress;
        await f.Context.SaveChangesAsync();
        TestingOperationResult result = await f.Service.ExecuteAsync(
            f.Execution(testCase, VModelTestResult.Pass, f.User(actor).Id));
        Assert.Equal(allowed, result.Succeeded);
        if (!allowed) Assert.Equal(TestingOperationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task FailedExecutionLinksOnlyUniqueSameProjectIssue()
    {
        await using var f = await Fixture.CreateAsync();
        VModelTestCase testCase = await f.TestCaseAsync(await f.ImplementationAsync());
        f.Phase(VModelPhaseType.UnitTesting).Status = VModelPhaseStatus.InProgress;
        await f.Context.SaveChangesAsync();
        int executionId = (await f.Service.ExecuteAsync(f.Execution(testCase, VModelTestResult.Fail))).ResourceId!.Value;
        Issue local = await f.IssueAsync(f.Project);
        Issue foreign = await f.IssueAsync(f.ForeignProject);
        Assert.True((await f.Service.LinkIssueAsync(f.IssueLink(executionId, local.Id))).Succeeded);
        Assert.Equal(TestingOperationFailure.Conflict, (await f.Service.LinkIssueAsync(f.IssueLink(executionId, local.Id))).Failure);
        Assert.Equal(TestingOperationFailure.Validation, (await f.Service.LinkIssueAsync(f.IssueLink(executionId, foreign.Id))).Failure);
        Assert.Single(f.Context.VModelTestExecutionIssues);
    }

    [Fact]
    public async Task PassingExecutionCannotLinkIssue()
    {
        await using var f = await Fixture.CreateAsync();
        VModelTestCase testCase = await f.TestCaseAsync(await f.ImplementationAsync());
        f.Phase(VModelPhaseType.UnitTesting).Status = VModelPhaseStatus.InProgress;
        await f.Context.SaveChangesAsync();
        int executionId = (await f.Service.ExecuteAsync(f.Execution(testCase, VModelTestResult.Pass))).ResourceId!.Value;
        Assert.Equal(TestingOperationFailure.Validation,
            (await f.Service.LinkIssueAsync(f.IssueLink(executionId, (await f.IssueAsync(f.Project)).Id))).Failure);
    }

    [Fact]
    public async Task OnlyCompletedPhasesCanBeValidatedAndHistoryIsAppendOnly()
    {
        await using var f = await Fixture.CreateAsync();
        VModelPhase phase = f.Phase(VModelPhaseType.Requirements);
        Assert.Equal(TestingOperationFailure.Conflict,
            (await f.Service.ValidatePhaseAsync(f.Validation(phase, VModelValidationResult.Pass))).Failure);
        phase.Status = VModelPhaseStatus.InProgress;
        await f.Context.SaveChangesAsync();
        Assert.Equal(TestingOperationFailure.Conflict,
            (await f.Service.ValidatePhaseAsync(f.Validation(phase, VModelValidationResult.Pass))).Failure);
        phase.Status = VModelPhaseStatus.Completed;
        await f.Context.SaveChangesAsync();
        Assert.True((await f.Service.ValidatePhaseAsync(f.Validation(phase, VModelValidationResult.Fail))).Succeeded);
        Assert.True((await f.Service.ValidatePhaseAsync(f.Validation(phase, VModelValidationResult.Pass))).Succeeded);
        Assert.Equal(2, await f.Context.VModelPhaseValidations.CountAsync(x => x.VModelPhaseId == phase.Id));
        Assert.Equal(VModelPhaseStatus.Completed, phase.Status);
        Assert.Equal(VModelValidationResult.Pass,
            (await f.Service.GetOverviewAsync(f.Project.Id))!.Phases.Single(x => x.Id == phase.Id).LatestValidationResult);
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("qa", true)]
    [InlineData("manager", false)]
    [InlineData("developer", false)]
    [InlineData("scrum", false)]
    public async Task ValidationAuthorizationIsExact(string actor, bool allowed)
    {
        await using var f = await Fixture.CreateAsync();
        VModelPhase phase = f.Phase(VModelPhaseType.Requirements);
        phase.Status = VModelPhaseStatus.Completed;
        await f.Context.SaveChangesAsync();
        TestingOperationResult result = await f.Service.ValidatePhaseAsync(
            f.Validation(phase, VModelValidationResult.Pass, f.User(actor).Id));
        Assert.Equal(allowed, result.Succeeded);
        if (!allowed) Assert.Equal(TestingOperationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task CoverageStateFollowsRealLatestRecordsAndManualTracesStaySeparate()
    {
        await using var f = await Fixture.CreateAsync();
        Requirement requirement = await f.RequirementAsync(f.Project);
        var requirements = new RequirementService(f.Context, f.Clock);
        Assert.Equal(RequirementCoverageState.MissingDesign, await Coverage());
        DesignArtifact design = await f.DesignAsync(f.Project);
        f.Context.Add(new DesignArtifactRequirement { DesignArtifactId = design.Id, RequirementId = requirement.Id });
        await f.Context.SaveChangesAsync();
        Assert.Equal(RequirementCoverageState.MissingImplementation, await Coverage());
        ImplementationArtifact implementation = await f.ImplementationAsync(f.Project, design);
        Assert.Equal(RequirementCoverageState.MissingTest, await Coverage());
        VModelTestCase testCase = await f.TestCaseAsync(implementation);
        Assert.Equal(RequirementCoverageState.NotVerified, await Coverage());
        f.Phase(VModelPhaseType.UnitTesting).Status = VModelPhaseStatus.InProgress;
        await f.Context.SaveChangesAsync();
        await f.Service.ExecuteAsync(f.Execution(testCase, VModelTestResult.Fail));
        Assert.Equal(RequirementCoverageState.VerificationFailed, await Coverage());
        await f.Service.ExecuteAsync(f.Execution(testCase, VModelTestResult.Pass));
        Assert.Equal(RequirementCoverageState.Verified, await Coverage());
        VModelPhase acceptance = f.Phase(VModelPhaseType.AcceptanceValidation);
        acceptance.Status = VModelPhaseStatus.Completed;
        f.Context.Add(new RequirementTrace { RequirementId = requirement.Id, Stage = RequirementTraceStage.Verification,
            ReferenceCode = "TEST-DOC-04", CreatedByUserId = f.Manager.Id, CreatedAt = f.Clock.UtcNow });
        await f.Context.SaveChangesAsync();
        Assert.Equal(RequirementCoverageState.ValidationPending, await Coverage());
        await f.Service.ValidatePhaseAsync(f.Validation(acceptance, VModelValidationResult.Fail));
        Assert.Equal(RequirementCoverageState.ValidationFailed, await Coverage());
        await f.Service.ValidatePhaseAsync(f.Validation(acceptance, VModelValidationResult.Pass));
        RequirementTraceabilityItem final = (await requirements.GetTraceabilityAsync(f.Project.Id)).Single();
        Assert.Equal(RequirementCoverageState.FullyValidated, final.CoverageState);
        Assert.Single(final.Traces);
        Assert.Equal("TEST-DOC-04", final.Traces[0].ReferenceCode);
        Assert.Single(final.DesignArtifacts.Single().ImplementationArtifacts.Single().TestCases);
        async Task<RequirementCoverageState> Coverage() =>
            (await requirements.GetTraceabilityAsync(f.Project.Id)).Single().CoverageState;
    }

    [Fact]
    public async Task ArchivedProjectRejectsTestExecutionAndValidation()
    {
        await using var f = await Fixture.CreateAsync();
        ImplementationArtifact implementation = await f.ImplementationAsync(f.ArchivedProject);
        VModelTestCase testCase = await f.DirectTestCaseAsync(f.ArchivedProject, implementation);
        VModelPhase phase = f.Phase(VModelPhaseType.UnitTesting, f.ArchivedProject.Id);
        phase.Status = VModelPhaseStatus.Completed;
        await f.Context.SaveChangesAsync();
        Assert.Equal(TestingOperationFailure.ReadOnly,
            (await f.Service.ExecuteAsync(f.Execution(testCase, VModelTestResult.Pass, f.Admin.Id, f.ArchivedProject.Id))).Failure);
        Assert.Equal(TestingOperationFailure.ReadOnly,
            (await f.Service.ValidatePhaseAsync(f.Validation(phase, VModelValidationResult.Pass, f.Admin.Id, f.ArchivedProject.Id))).Failure);
    }

    [Fact]
    public async Task RoutesDenyNonmemberAndRejectScrumMethodology()
    {
        await using var f = await Fixture.CreateAsync();
        IActionResult outsider = await f.Controller(f.Outsider).Index(f.Project.Id, null, null, false, default);
        IActionResult scrum = await f.Controller(f.Admin).Index(f.ScrumProject.Id, null, null, false, default);
        Assert.IsType<ForbidResult>(outsider);
        Assert.IsType<NotFoundResult>(scrum);
    }

    [Fact]
    public async Task ForgedEnumsIdsLengthsAndProjectLinksAreRejectedAndAudited()
    {
        await using var f = await Fixture.CreateAsync();
        ImplementationArtifact implementation = await f.ImplementationAsync();
        Assert.Equal(TestingOperationFailure.Validation,
            (await f.Service.CreateTestCaseAsync(f.TestRequest(f.Qa.Id, [implementation.Id], level: (VModelTestLevel)999))).Failure);
        Assert.Equal(TestingOperationFailure.Validation,
            (await f.Service.CreateTestCaseAsync(f.TestRequest(f.Qa.Id, [implementation.Id], title: new string('x', 201)))).Failure);
        Assert.Equal(TestingOperationFailure.NotFound,
            (await f.Service.UpdateTestCaseAsync(new UpdateVModelTestCaseRequest { ProjectId = f.ForeignProject.Id,
                TestCaseId = int.MaxValue, ActorUserId = f.Admin.Id, Title = "x", Description = "x",
                TestLevel = VModelTestLevel.Unit, TestSteps = "x", ExpectedResult = "x",
                ImplementationArtifactIds = [implementation.Id] })).Failure);
        VModelTestCase testCase = await f.TestCaseAsync(implementation);
        f.Phase(VModelPhaseType.UnitTesting).Status = VModelPhaseStatus.InProgress;
        await f.Context.SaveChangesAsync();
        await f.Service.ExecuteAsync(f.Execution(testCase, VModelTestResult.Pass));
        Assert.Contains(f.Context.ActivityLogs, x => x.Action == "VModelTestCaseCreated");
        Assert.Contains(f.Context.ActivityLogs, x => x.Action == "VModelTestCaseImplementationLinked");
        Assert.Contains(f.Context.ActivityLogs, x => x.Action == "VModelTestExecuted");
    }

    [Fact]
    public void WebFormsKeepAntiforgeryEncodingAndOverpostingProtection()
    {
        string root = RepositoryRoot();
        string directory = Path.Combine(root, "src", "Planora.Web", "Views", "VModelTesting");
        string views = string.Join('\n', Directory.GetFiles(directory, "*.cshtml").Select(File.ReadAllText));
        Assert.DoesNotContain("Html.Raw", views, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AntiForgeryToken", views);
        Assert.DoesNotContain("CreatedByUserId", views);
        Assert.DoesNotContain("ExecutedByUserId", views);
        Assert.DoesNotContain("ValidatedByUserId", views);
    }

    private static CreateProjectRequest NewProjectRequest(string name, ProjectMethodology methodology, string actor) =>
        new() { Name = name, Description = "Description", Methodology = methodology,
            StartDate = DateTime.UtcNow, CreatedByUserId = actor };

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Planora.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        readonly ServiceProvider provider; readonly IServiceScope scope;
        public ApplicationDbContext Context { get; }
        public VModelTestingService Service { get; }
        public Clock Clock { get; } = new();
        public ApplicationUser Admin { get; }
        public ApplicationUser Manager { get; }
        public ApplicationUser Qa { get; }
        public ApplicationUser Developer { get; }
        public ApplicationUser ScrumMaster { get; }
        public ApplicationUser Outsider { get; }
        public Project Project { get; }
        public Project ForeignProject { get; }
        public Project ScrumProject { get; }
        public Project ArchivedProject { get; }

        Fixture(ServiceProvider provider, bool seedPhases)
        {
            this.provider = provider; scope = provider.CreateScope();
            Context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Service = scope.ServiceProvider.GetRequiredService<VModelTestingService>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (string role in SystemRoles.All) Assert.True(roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult().Succeeded);
            Admin = AddUser(users, "admin", SystemRoles.Admin); Manager = AddUser(users, "manager", SystemRoles.ProjectManager);
            Qa = AddUser(users, "qa", SystemRoles.QaTester); Developer = AddUser(users, "developer", SystemRoles.Developer);
            ScrumMaster = AddUser(users, "scrum", SystemRoles.ScrumMaster); Outsider = AddUser(users, "outsider", SystemRoles.QaTester);
            Project = AddProject("V Model", ProjectMethodology.VModel, ProjectStatus.Active);
            ForeignProject = AddProject("Foreign", ProjectMethodology.VModel, ProjectStatus.Active);
            ScrumProject = AddProject("Scrum", ProjectMethodology.Scrum, ProjectStatus.Active);
            ArchivedProject = AddProject("Archived", ProjectMethodology.VModel, ProjectStatus.Archived);
            Context.AddRange(Project, ForeignProject, ScrumProject, ArchivedProject); Context.SaveChanges();
            Context.AddRange(Member(Manager, ProjectMemberRole.ProjectManager), Member(Qa, ProjectMemberRole.QaTester),
                Member(Developer, ProjectMemberRole.Developer), Member(ScrumMaster, ProjectMemberRole.ScrumMaster));
            if (seedPhases)
                foreach (Project project in new[] { Project, ForeignProject, ArchivedProject })
                    foreach ((VModelPhaseType type, int index) in VModelPhaseCatalog.Ordered.Select((type, index) => (type, index)))
                        Context.Add(new VModelPhase { ProjectId = project.Id, PhaseType = type, PhaseOrder = index + 1,
                            Status = VModelPhaseStatus.NotStarted, CreatedAt = Clock.UtcNow });
            Context.SaveChanges();
        }

        public static Task<Fixture> CreateAsync(bool seedPhases = true)
        {
            var services = new ServiceCollection(); var root = new InMemoryDatabaseRoot(); string name = Guid.NewGuid().ToString();
            services.AddLogging(); services.AddDbContext<ApplicationDbContext>(x => x.UseInMemoryDatabase(name, root));
            services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton<IClock, Clock>(); services.AddScoped<VModelTestingService>();
            return Task.FromResult(new Fixture(services.BuildServiceProvider(), seedPhases));
        }

        public IServiceScope Scope() => provider.CreateScope();
        public ApplicationUser User(string key) => key switch { "admin" => Admin, "manager" => Manager,
            "qa" => Qa, "developer" => Developer, "scrum" => ScrumMaster, _ => Outsider };
        public VModelPhase Phase(VModelPhaseType type, int? projectId = null) => Context.VModelPhases.Local
            .Single(x => x.ProjectId == (projectId ?? Project.Id) && x.PhaseType == type);
        public TransitionPhaseRequest Transition(VModelPhase p, VModelPhaseStatus expected, VModelPhaseStatus target,
            string? actor = null, int? projectId = null) => new() { ProjectId = projectId ?? Project.Id, PhaseId = p.Id,
                ExpectedStatus = expected, TargetStatus = target, ActorUserId = actor ?? Manager.Id };
        public CreateVModelTestCaseRequest TestRequest(string actor, IReadOnlyList<int> implementations,
            string title = "Authentication unit test", int? projectId = null, VModelTestLevel level = VModelTestLevel.Unit) =>
            new() { ProjectId = projectId ?? Project.Id, ActorUserId = actor, Title = title, Description = "Description",
                TestLevel = level, Preconditions = "Configured", TestSteps = "Run the test", ExpectedResult = "Pass",
                ImplementationArtifactIds = implementations };
        public UpdateVModelTestCaseRequest Update(VModelTestCase item, IReadOnlyList<int> ids, string? title = null) =>
            new() { ProjectId = item.ProjectId, TestCaseId = item.Id, ActorUserId = Qa.Id,
                Title = title ?? item.Title, Description = item.Description, TestLevel = item.TestLevel,
                Preconditions = item.Preconditions, TestSteps = item.TestSteps, ExpectedResult = item.ExpectedResult,
                ImplementationArtifactIds = ids };
        public ExecuteVModelTestCaseRequest Execution(VModelTestCase item, VModelTestResult result,
            string? actor = null, int? projectId = null) => new() { ProjectId = projectId ?? Project.Id,
                TestCaseId = item.Id, Result = result, ActualResult = "Observed result", Notes = "Notes",
                ActorUserId = actor ?? Qa.Id };
        public ValidateVModelPhaseRequest Validation(VModelPhase phase, VModelValidationResult result,
            string? actor = null, int? projectId = null) => new() { ProjectId = projectId ?? Project.Id,
                PhaseId = phase.Id, Result = result, Notes = "Validation notes", ActorUserId = actor ?? Qa.Id };
        public LinkVModelTestIssueRequest IssueLink(int executionId, int issueId) => new()
            { ProjectId = Project.Id, ExecutionId = executionId, IssueId = issueId, ActorUserId = Qa.Id };

        public async Task<Requirement> RequirementAsync(Project project)
        {
            var value = new Requirement { ProjectId = project.Id, Identifier = "FR-001", Title = "Authentication",
                Description = "Authenticate users", Type = RequirementType.Functional, Priority = PriorityLevel.High,
                Status = RequirementStatus.Approved, CreatedByUserId = Manager.Id, CreatedAt = Clock.UtcNow };
            Context.Add(value); await Context.SaveChangesAsync(); return value;
        }
        public async Task<DesignArtifact> DesignAsync(Project? project = null)
        {
            project ??= Project; var value = new DesignArtifact { ProjectId = project.Id, Identifier = "DES-001",
                Title = "Architecture", Description = "Design", Type = DesignArtifactType.ArchitectureDesign,
                CreatedByUserId = Admin.Id, CreatedAt = Clock.UtcNow };
            Context.Add(value); await Context.SaveChangesAsync(); return value;
        }
        public async Task<ImplementationArtifact> ImplementationAsync(Project? project = null, DesignArtifact? design = null)
        {
            project ??= Project; design ??= await DesignAsync(project);
            var value = new ImplementationArtifact { ProjectId = project.Id, Identifier = "IMP-001",
                Title = "Service", Description = "Implementation", Type = ImplementationArtifactType.Service,
                CreatedByUserId = Developer.Id, CreatedAt = Clock.UtcNow };
            Context.Add(value); await Context.SaveChangesAsync();
            Context.Add(new ImplementationArtifactDesign { ImplementationArtifactId = value.Id, DesignArtifactId = design.Id });
            await Context.SaveChangesAsync(); return value;
        }
        public async Task<VModelTestCase> TestCaseAsync(ImplementationArtifact implementation)
        {
            TestingOperationResult result = await Service.CreateTestCaseAsync(TestRequest(Qa.Id, [implementation.Id]));
            Assert.True(result.Succeeded, result.Message); return await Context.VModelTestCases.SingleAsync(x => x.Id == result.ResourceId);
        }
        public async Task<VModelTestCase> DirectTestCaseAsync(Project project, ImplementationArtifact implementation)
        {
            var value = new VModelTestCase { ProjectId = project.Id, Identifier = "TC-001", Title = "Test",
                Description = "Description", TestLevel = VModelTestLevel.Unit, TestSteps = "Steps",
                ExpectedResult = "Expected", CreatedByUserId = Admin.Id, CreatedAt = Clock.UtcNow };
            Context.Add(value); await Context.SaveChangesAsync(); Context.Add(new VModelTestCaseImplementationArtifact
                { VModelTestCaseId = value.Id, ImplementationArtifactId = implementation.Id });
            await Context.SaveChangesAsync(); return value;
        }
        public async Task<Issue> IssueAsync(Project project)
        {
            var issue = new Issue { ProjectId = project.Id, Title = $"Issue {project.Id}", Description = "Bug",
                ReporterUserId = Qa.Id, CreatedAt = Clock.UtcNow, Status = IssueStatus.Open };
            Context.Add(issue); await Context.SaveChangesAsync(); return issue;
        }
        public VModelTestingController Controller(ApplicationUser user)
        {
            var controller = new VModelTestingController(Service, new ProjectService(Context, Clock), new ProjectAccessService(Context));
            var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)], "Test");
            foreach (string role in Context.UserRoles.Where(x => x.UserId == user.Id)
                .Join(Context.Roles, x => x.RoleId, x => x.Id, (_, role) => role.Name!).ToList())
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            controller.ControllerContext = new ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                { User = new ClaimsPrincipal(identity) } };
            return controller;
        }

        Project AddProject(string name, ProjectMethodology methodology, ProjectStatus status) => new()
            { Name = name, Description = "Description", Objectives = "Objectives", Scope = "Scope",
                Methodology = methodology, Status = status, StartDate = Clock.UtcNow,
                CreatedByUserId = Admin.Id, CreatedAt = Clock.UtcNow };
        ProjectMember Member(ApplicationUser user, ProjectMemberRole role) => new()
            { ProjectId = Project.Id, UserId = user.Id, Role = role, JoinedAt = Clock.UtcNow };
        static ApplicationUser AddUser(UserManager<ApplicationUser> manager, string name, string role)
        {
            var user = new ApplicationUser { UserName = $"{name}@testing.test", Email = $"{name}@testing.test",
                FullName = name, EmailConfirmed = true };
            Assert.True(manager.CreateAsync(user).GetAwaiter().GetResult().Succeeded);
            Assert.True(manager.AddToRoleAsync(user, role).GetAwaiter().GetResult().Succeeded); return user;
        }
        public async ValueTask DisposeAsync() { scope.Dispose(); await provider.DisposeAsync(); }
    }

    public sealed class Clock : IClock { public DateTime UtcNow => new(2026, 9, 9, 9, 0, 0, DateTimeKind.Utc); }
}
