using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Requirements;
using Planora.Application.Common.Security;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;
using Planora.Infrastructure.Projects;
using Planora.Infrastructure.Requirements;
using Planora.Infrastructure.Security;
using Planora.Web.Controllers;
using Planora.Web.ViewModels.Requirements;

namespace Planora.IntegrationTests;

public sealed class RequirementManagementTests
{
    [Fact]
    public async Task IdentifiersAreGeneratedPerProjectAndType()
    {
        await using var fixture = await Fixture.CreateAsync();

        RequirementOperationResult firstFunctional = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.ProjectManager.Id, RequirementType.Functional));
        RequirementOperationResult secondFunctional = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.ProjectManager.Id, RequirementType.Functional, "Second FR"));
        RequirementOperationResult firstNonFunctional = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.ProjectManager.Id, RequirementType.NonFunctional));
        RequirementOperationResult foreignFunctional = await fixture.Service.CreateAsync(
            fixture.CreateRequest(
                fixture.Admin.Id,
                RequirementType.Functional,
                "Foreign FR",
                fixture.ForeignProject.Id));

        Assert.Equal("FR-001", firstFunctional.Identifier);
        Assert.Equal("FR-002", secondFunctional.Identifier);
        Assert.Equal("NFR-001", firstNonFunctional.Identifier);
        Assert.Equal("FR-001", foreignFunctional.Identifier);
    }

    [Fact]
    public async Task CreateContractDoesNotAcceptClientIdentifier()
    {
        await using var fixture = await Fixture.CreateAsync();

        RequirementOperationResult result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.ProjectManager.Id));

        Assert.True(result.Succeeded);
        Assert.Equal("FR-001", result.Identifier);
        Assert.Null(typeof(CreateRequirementRequest).GetProperty("Identifier"));
        Assert.Null(typeof(CreateRequirementViewModel).GetProperty("Identifier"));
    }

    [Fact]
    public async Task IdentifierAndTypeRemainImmutableDuringEdit()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync(
            RequirementStatus.Draft,
            RequirementType.NonFunctional,
            "NFR-001");

        RequirementOperationResult result = await fixture.Service.UpdateAsync(
            fixture.UpdateRequest(requirement, fixture.ProjectManager.Id, "Updated"));

        Assert.True(result.Succeeded);
        Assert.Equal("NFR-001", requirement.Identifier);
        Assert.Equal(RequirementType.NonFunctional, requirement.Type);
        Assert.Null(typeof(UpdateRequirementRequest).GetProperty("Identifier"));
        Assert.Null(typeof(UpdateRequirementRequest).GetProperty("Type"));
    }

    [Fact]
    public async Task FunctionalRequirementFieldsArePersisted()
    {
        await using var fixture = await Fixture.CreateAsync();
        CreateRequirementRequest source = fixture.CreateRequest(fixture.ProjectManager.Id);
        var request = new CreateRequirementRequest
        {
            ProjectId = source.ProjectId,
            Title = "Manage users",
            Description = "An Admin can search and manage registered users.",
            Type = RequirementType.Functional,
            Priority = PriorityLevel.High,
            Rationale = "Administrators need controlled account management.",
            Preconditions = "The authenticated user must have the Admin role.",
            ExceptionScenario = "If no users match, display: No matching users found.",
            ActorUserId = fixture.ProjectManager.Id
        };

        RequirementOperationResult result = await fixture.Service.CreateAsync(request);
        Requirement saved = await fixture.Context.Requirements.SingleAsync();

        Assert.True(result.Succeeded);
        Assert.Equal("FR-001", saved.Identifier);
        Assert.Equal(request.Title, saved.Title);
        Assert.Equal(request.Preconditions, saved.Preconditions);
        Assert.Equal(request.ExceptionScenario, saved.ExceptionScenario);
        Assert.Null(saved.NfrCategory);
    }

    [Fact]
    public async Task FunctionalRequirementRejectsNfrOnlyData()
    {
        await using var fixture = await Fixture.CreateAsync();
        CreateRequirementRequest source = fixture.CreateRequest(fixture.ProjectManager.Id);

        RequirementOperationResult result = await fixture.Service.CreateAsync(
            new CreateRequirementRequest
            {
                ProjectId = source.ProjectId,
                Title = source.Title,
                Description = source.Description,
                Type = RequirementType.Functional,
                Priority = source.Priority,
                NfrCategory = NfrCategory.Security,
                ActorUserId = source.ActorUserId
            });

        Assert.Equal(RequirementOperationFailure.Validation, result.Failure);
        Assert.Empty(fixture.Context.Requirements);
    }

    [Fact]
    public async Task TypeSpecificTextLengthAndCrossTypeFieldsAreValidated()
    {
        await using var fixture = await Fixture.CreateAsync();
        CreateRequirementRequest functional = fixture.CreateRequest(fixture.ProjectManager.Id);

        RequirementOperationResult longPreconditions = await fixture.Service.CreateAsync(
            new CreateRequirementRequest
            {
                ProjectId = functional.ProjectId,
                Title = functional.Title,
                Description = functional.Description,
                Type = functional.Type,
                Priority = functional.Priority,
                Preconditions = new string('x', 2001),
                ActorUserId = functional.ActorUserId
            });
        RequirementOperationResult invalidCategory = await fixture.Service.CreateAsync(
            fixture.CreateNfrRequest(
                fixture.ProjectManager.Id,
                category: (NfrCategory)999));
        CreateRequirementRequest nfr = fixture.CreateNfrRequest(fixture.ProjectManager.Id);
        RequirementOperationResult nfrWithFunctionalField = await fixture.Service.CreateAsync(
            new CreateRequirementRequest
            {
                ProjectId = nfr.ProjectId,
                Description = nfr.Description,
                Type = nfr.Type,
                Priority = nfr.Priority,
                NfrCategory = nfr.NfrCategory,
                Preconditions = "Forged prerequisite",
                ActorUserId = nfr.ActorUserId
            });

        Assert.Equal(RequirementOperationFailure.Validation, longPreconditions.Failure);
        Assert.Equal(RequirementOperationFailure.Validation, invalidCategory.Failure);
        Assert.Equal(RequirementOperationFailure.Validation, nfrWithFunctionalField.Failure);
    }

    [Fact]
    public async Task FunctionalDetailsExposeFunctionalFieldsSafely()
    {
        await using var fixture = await Fixture.CreateAsync();
        CreateRequirementRequest source = fixture.CreateRequest(fixture.ProjectManager.Id);
        RequirementOperationResult created = await fixture.Service.CreateAsync(
            new CreateRequirementRequest
            {
                ProjectId = source.ProjectId,
                Title = source.Title,
                Description = source.Description,
                Type = source.Type,
                Priority = source.Priority,
                Rationale = source.Rationale,
                Preconditions = "Admin role required.",
                ExceptionScenario = "Show an empty result message.",
                ActorUserId = source.ActorUserId
            });

        RequirementDetails? details = await fixture.Service.GetByIdAsync(
            fixture.Project.Id, created.RequirementId!.Value);
        string view = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "Planora.Web", "Views", "Requirements", "Details.cshtml"));

        Assert.NotNull(details);
        Assert.Equal("Admin role required.", details.Preconditions);
        Assert.Equal("Show an empty result message.", details.ExceptionScenario);
        Assert.Contains("Dependency / Preconditions", view);
        Assert.Contains("Exception Scenario", view);
        Assert.DoesNotContain("Html.Raw", view, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NonFunctionalCategoryIsRequiredAndPersists()
    {
        await using var fixture = await Fixture.CreateAsync();

        RequirementOperationResult missing = await fixture.Service.CreateAsync(
            fixture.CreateNfrRequest(fixture.ProjectManager.Id, category: null));
        RequirementOperationResult valid = await fixture.Service.CreateAsync(
            fixture.CreateNfrRequest(
                fixture.ProjectManager.Id,
                category: NfrCategory.Performance));
        Requirement saved = await fixture.Context.Requirements.SingleAsync();

        Assert.Equal(RequirementOperationFailure.Validation, missing.Failure);
        Assert.True(valid.Succeeded);
        Assert.Equal("NFR-001", saved.Identifier);
        Assert.Equal(NfrCategory.Performance, saved.NfrCategory);
        Assert.False(string.IsNullOrWhiteSpace(saved.Title));
    }

    [Fact]
    public async Task NonFunctionalRequirementCanRelateToMultipleSameProjectFunctionalRequirements()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement first = await fixture.SeedRequirementAsync(identifier: "FR-001");
        Requirement second = await fixture.SeedRequirementAsync(identifier: "FR-002");

        RequirementOperationResult result = await fixture.Service.CreateAsync(
            fixture.CreateNfrRequest(
                fixture.ProjectManager.Id,
                [first.Id, second.Id]));

        Assert.True(result.Succeeded);
        Assert.Equal("NFR-001", result.Identifier);
        Assert.Equal(2, fixture.Context.RequirementDependencies.Count());
        Assert.All(
            fixture.Context.RequirementDependencies,
            link => Assert.Contains(link.DependsOnRequirementId, new[] { first.Id, second.Id }));
    }

    [Fact]
    public async Task CrossProjectAndNonFunctionalRelatedRequirementsAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement foreignFunctional = await fixture.SeedRequirementAsync(
            identifier: "FR-001", project: fixture.ForeignProject);
        Requirement sameProjectNfr = await fixture.SeedRequirementAsync(
            type: RequirementType.NonFunctional,
            identifier: "NFR-001");

        RequirementOperationResult crossProject = await fixture.Service.CreateAsync(
            fixture.CreateNfrRequest(
                fixture.ProjectManager.Id,
                [foreignFunctional.Id]));
        RequirementOperationResult nfrSelection = await fixture.Service.CreateAsync(
            fixture.CreateNfrRequest(
                fixture.ProjectManager.Id,
                [sameProjectNfr.Id]));

        Assert.Equal(RequirementOperationFailure.Validation, crossProject.Failure);
        Assert.Equal(RequirementOperationFailure.Validation, nfrSelection.Failure);
    }

    [Fact]
    public async Task DuplicateRelatedFunctionalRequirementIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement functional = await fixture.SeedRequirementAsync(identifier: "FR-001");

        RequirementOperationResult result = await fixture.Service.CreateAsync(
            fixture.CreateNfrRequest(
                fixture.ProjectManager.Id,
                [functional.Id, functional.Id]));

        Assert.Equal(RequirementOperationFailure.Validation, result.Failure);
        Assert.Empty(fixture.Context.RequirementDependencies);
    }

    [Fact]
    public async Task NfrEditPreservesAndDisplaysSelectedFunctionalRequirements()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement first = await fixture.SeedRequirementAsync(identifier: "FR-001");
        Requirement second = await fixture.SeedRequirementAsync(identifier: "FR-002");
        RequirementOperationResult created = await fixture.Service.CreateAsync(
            fixture.CreateNfrRequest(
                fixture.ProjectManager.Id,
                [first.Id, second.Id]));
        Requirement nfr = await fixture.Context.Requirements
            .SingleAsync(item => item.Id == created.RequirementId);

        RequirementOperationResult updated = await fixture.Service.UpdateAsync(
            fixture.UpdateNfrRequest(nfr, [first.Id, second.Id]));
        RequirementsController controller = fixture.CreateController(fixture.ProjectManager);
        ViewResult edit = Assert.IsType<ViewResult>(await controller.Edit(
            fixture.Project.Id, nfr.Id, default));
        var model = Assert.IsType<EditRequirementViewModel>(edit.Model);

        Assert.True(updated.Succeeded);
        Assert.Equal([first.Id, second.Id], model.RelatedFunctionalRequirementIds.Order().ToArray());
        Assert.Equal(2, model.FunctionalRequirementOptions.Count);
    }

    [Fact]
    public async Task InvalidNfrCreatePreservesSelectedFormAndRelationships()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement functional = await fixture.SeedRequirementAsync(identifier: "FR-001");
        RequirementsController controller = fixture.CreateController(fixture.ProjectManager);
        var model = new CreateRequirementViewModel
        {
            ProjectId = fixture.Project.Id,
            Type = RequirementType.NonFunctional,
            Description = "A preserved NFR description.",
            Priority = PriorityLevel.High,
            NfrCategory = null,
            RelatedFunctionalRequirementIds = [functional.Id]
        };

        ViewResult result = Assert.IsType<ViewResult>(await controller.Create(
            fixture.Project.Id, model, default));
        var returned = Assert.IsType<CreateRequirementViewModel>(result.Model);

        Assert.Equal(RequirementType.NonFunctional, returned.Type);
        Assert.Equal("A preserved NFR description.", returned.Description);
        Assert.Equal([functional.Id], returned.RelatedFunctionalRequirementIds);
        Assert.Single(returned.FunctionalRequirementOptions);
    }

    [Fact]
    public async Task NfrEditRemovesOnlyDeselectedFunctionalRelationships()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement functional = await fixture.SeedRequirementAsync(identifier: "FR-001");
        RequirementOperationResult created = await fixture.Service.CreateAsync(
            fixture.CreateNfrRequest(fixture.ProjectManager.Id, [functional.Id]));
        Requirement nfr = await fixture.Context.Requirements
            .SingleAsync(item => item.Id == created.RequirementId);
        Requirement legacyNfr = await fixture.SeedRequirementAsync(
            type: RequirementType.NonFunctional,
            identifier: "NFR-002");
        fixture.Context.RequirementDependencies.Add(new RequirementDependency
        {
            RequirementId = nfr.Id,
            DependsOnRequirementId = legacyNfr.Id,
            CreatedByUserId = fixture.ProjectManager.Id,
            CreatedAt = new DateTime(2026, 9, 8, 12, 0, 0, DateTimeKind.Utc)
        });
        await fixture.Context.SaveChangesAsync();

        RequirementOperationResult result = await fixture.Service.UpdateAsync(
            fixture.UpdateNfrRequest(nfr, []));
        int[] remainingTargets = await fixture.Context.RequirementDependencies
            .Where(link => link.RequirementId == nfr.Id)
            .Select(link => link.DependsOnRequirementId)
            .ToArrayAsync();

        Assert.True(result.Succeeded);
        Assert.Equal([legacyNfr.Id], remainingTargets);
    }

    [Fact]
    public async Task NfrDetailsDisplayCategoryAndRelatedFunctionalRequirements()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement functional = await fixture.SeedRequirementAsync(identifier: "FR-001");
        RequirementOperationResult created = await fixture.Service.CreateAsync(
            fixture.CreateNfrRequest(
                fixture.ProjectManager.Id,
                [functional.Id],
                NfrCategory.Availability));

        RequirementDetails? details = await fixture.Service.GetByIdAsync(
            fixture.Project.Id, created.RequirementId!.Value);
        string view = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "Planora.Web", "Views", "Requirements", "Details.cshtml"));

        Assert.NotNull(details);
        Assert.Equal(NfrCategory.Availability, details.NfrCategory);
        Assert.Single(details.RelatedFunctionalRequirements);
        Assert.Contains("Related Functional Requirements", view);
        Assert.Contains("NFR Category", view);
    }

    [Fact]
    public async Task ForgedEditTypeCannotChangePersistedRequirementType()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync();
        RequirementsController controller = fixture.CreateController(fixture.ProjectManager);
        controller.TempData = new TempDataDictionary(
            controller.HttpContext,
            new MemoryTempDataProvider());
        var forged = new EditRequirementViewModel
        {
            Id = requirement.Id,
            ProjectId = fixture.Project.Id,
            Type = RequirementType.NonFunctional,
            NfrCategory = NfrCategory.Security,
            ExpectedStatus = requirement.Status,
            Title = "Updated functional requirement",
            Description = requirement.Description,
            Priority = requirement.Priority
        };

        IActionResult result = await controller.Edit(
            fixture.Project.Id, requirement.Id, forged, default);

        Assert.IsType<ViewResult>(result);
        Assert.Equal(RequirementType.Functional, requirement.Type);
        Assert.Null(requirement.NfrCategory);
    }

    [Fact]
    public async Task ConcurrentCreationAllocatesUniqueIdentifiers()
    {
        await using var fixture = await Fixture.CreateAsync();
        using IServiceScope firstScope = fixture.CreateScope();
        using IServiceScope secondScope = fixture.CreateScope();
        var firstService = firstScope.ServiceProvider.GetRequiredService<RequirementService>();
        var secondService = secondScope.ServiceProvider.GetRequiredService<RequirementService>();

        Task<RequirementOperationResult> first = firstService.CreateAsync(
            fixture.CreateRequest(fixture.ProjectManager.Id, title: "Concurrent A"));
        Task<RequirementOperationResult> second = secondService.CreateAsync(
            fixture.CreateRequest(fixture.ProjectManager.Id, title: "Concurrent B"));
        RequirementOperationResult[] results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(
            ["FR-001", "FR-002"],
            results.Select(result => result.Identifier!).Order().ToArray());
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("manager")]
    public async Task AdminAndMatchingProjectManagerCanCreate(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();

        RequirementOperationResult result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.Actor(actor).Id));

        Assert.True(result.Succeeded);
        Assert.Contains(fixture.Context.ActivityLogs,
            log => log.Action == "RequirementCreated");
    }

    [Theory]
    [InlineData("scrum")]
    [InlineData("developer")]
    [InlineData("qa")]
    public async Task ViewOnlyRolesCannotCreate(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();

        RequirementOperationResult result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.Actor(actor).Id));

        Assert.Equal(RequirementOperationFailure.Forbidden, result.Failure);
    }

    [Theory]
    [InlineData("scrum")]
    [InlineData("developer")]
    [InlineData("qa")]
    public async Task AuthorizedMembersCanViewButCannotOpenCreate(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();
        RequirementsController controller = fixture.CreateController(fixture.Actor(actor));

        IActionResult index = await controller.Index(
            fixture.Project.Id, null, null, null, null, default);
        IActionResult create = await controller.Create(fixture.Project.Id, default);

        Assert.IsType<ViewResult>(index);
        Assert.IsType<ForbidResult>(create);
    }

    [Fact]
    public async Task NonMemberCannotViewOrCreateRequirements()
    {
        await using var fixture = await Fixture.CreateAsync();
        RequirementsController controller = fixture.CreateController(fixture.Outsider);

        IActionResult index = await controller.Index(
            fixture.Project.Id, null, null, null, null, default);
        RequirementOperationResult create = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.Outsider.Id));

        Assert.IsType<ForbidResult>(index);
        Assert.Equal(RequirementOperationFailure.Forbidden, create.Failure);
    }

    [Fact]
    public async Task ScrumProjectRejectsRequirementRoutesAndCreation()
    {
        await using var fixture = await Fixture.CreateAsync();
        RequirementsController controller = fixture.CreateController(fixture.Admin);

        IActionResult index = await controller.Index(
            fixture.ScrumProject.Id, null, null, null, null, default);
        IActionResult traceability = await controller.Traceability(
            fixture.ScrumProject.Id, default);
        RequirementOperationResult create = await fixture.Service.CreateAsync(
            fixture.CreateRequest(
                fixture.Admin.Id,
                projectId: fixture.ScrumProject.Id));

        Assert.IsType<NotFoundResult>(index);
        Assert.IsType<NotFoundResult>(traceability);
        Assert.Equal(RequirementOperationFailure.Forbidden, create.Failure);
    }

    [Fact]
    public async Task ArchivedVModelProjectIsViewOnly()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync();
        fixture.Project.Status = ProjectStatus.Archived;
        await fixture.Context.SaveChangesAsync();
        RequirementsController controller = fixture.CreateController(fixture.ProjectManager);

        IActionResult index = await controller.Index(
            fixture.Project.Id, null, null, null, null, default);
        RequirementOperationResult create = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.ProjectManager.Id));
        RequirementOperationResult update = await fixture.Service.UpdateAsync(
            fixture.UpdateRequest(requirement, fixture.ProjectManager.Id, "Blocked"));

        Assert.IsType<ViewResult>(index);
        Assert.Equal(RequirementOperationFailure.ReadOnly, create.Failure);
        Assert.Equal(RequirementOperationFailure.ReadOnly, update.Failure);
    }

    [Fact]
    public async Task ArchivedProjectRejectsStatusDependencyAndTraceMutations()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement source = await fixture.SeedRequirementAsync(identifier: "FR-001");
        Requirement target = await fixture.SeedRequirementAsync(identifier: "FR-002");
        fixture.Project.Status = ProjectStatus.Archived;
        await fixture.Context.SaveChangesAsync();

        RequirementOperationResult transition = await fixture.Service.TransitionAsync(
            fixture.Transition(source, fixture.ProjectManager.Id, RequirementStatus.UnderReview));
        RequirementOperationResult dependency = await fixture.Service.AddDependencyAsync(
            fixture.Dependency(source, target, fixture.ProjectManager.Id));
        RequirementOperationResult trace = await fixture.Service.AddTraceAsync(
            fixture.Trace(source, fixture.ProjectManager.Id));

        Assert.Equal(RequirementOperationFailure.ReadOnly, transition.Failure);
        Assert.Equal(RequirementOperationFailure.ReadOnly, dependency.Failure);
        Assert.Equal(RequirementOperationFailure.ReadOnly, trace.Failure);
    }

    [Theory]
    [InlineData(RequirementStatus.Draft, RequirementStatus.UnderReview)]
    [InlineData(RequirementStatus.UnderReview, RequirementStatus.Approved)]
    [InlineData(RequirementStatus.UnderReview, RequirementStatus.Rejected)]
    [InlineData(RequirementStatus.Rejected, RequirementStatus.Draft)]
    [InlineData(RequirementStatus.Approved, RequirementStatus.Deprecated)]
    public async Task ApprovedStatusTransitionsSucceed(
        RequirementStatus source,
        RequirementStatus target)
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync(source);

        RequirementOperationResult result = await fixture.Service.TransitionAsync(
            fixture.Transition(requirement, fixture.ProjectManager.Id, target));

        Assert.True(result.Succeeded);
        Assert.Equal(target, requirement.Status);
        Assert.Contains(fixture.Context.ActivityLogs,
            log => log.Action == "RequirementStatusChanged");
    }

    [Theory]
    [InlineData(RequirementStatus.Draft, RequirementStatus.Approved)]
    [InlineData(RequirementStatus.Draft, RequirementStatus.Rejected)]
    [InlineData(RequirementStatus.UnderReview, RequirementStatus.Draft)]
    [InlineData(RequirementStatus.Rejected, RequirementStatus.Approved)]
    [InlineData(RequirementStatus.Approved, RequirementStatus.UnderReview)]
    [InlineData(RequirementStatus.Deprecated, RequirementStatus.Draft)]
    public async Task UnsupportedStatusTransitionsAreRejected(
        RequirementStatus source,
        RequirementStatus target)
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync(source);

        RequirementOperationResult result = await fixture.Service.TransitionAsync(
            fixture.Transition(requirement, fixture.ProjectManager.Id, target));

        Assert.Equal(RequirementOperationFailure.Validation, result.Failure);
        Assert.Equal(source, requirement.Status);
    }

    [Fact]
    public async Task EveryUndefinedStatusTransitionIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        int sequence = 1;

        foreach (RequirementStatus source in Enum.GetValues<RequirementStatus>())
        foreach (RequirementStatus target in Enum.GetValues<RequirementStatus>())
        {
            if (IsApprovedTransition(source, target))
                continue;

            Requirement requirement = await fixture.SeedRequirementAsync(
                source,
                identifier: $"FR-{sequence++:D3}");
            RequirementOperationResult result = await fixture.Service.TransitionAsync(
                fixture.Transition(requirement, fixture.ProjectManager.Id, target));

            Assert.Equal(RequirementOperationFailure.Validation, result.Failure);
            Assert.Equal(source, requirement.Status);
        }
    }

    [Theory]
    [InlineData("scrum")]
    [InlineData("developer")]
    [InlineData("qa")]
    public async Task ViewOnlyRolesCannotTransitionRequirement(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync();

        RequirementOperationResult result = await fixture.Service.TransitionAsync(
            fixture.Transition(
                requirement,
                fixture.Actor(actor).Id,
                RequirementStatus.UnderReview));

        Assert.Equal(RequirementOperationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task StaleStatusTransitionIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync();
        TransitionRequirementRequest request = fixture.Transition(
            requirement, fixture.ProjectManager.Id, RequirementStatus.UnderReview);

        RequirementOperationResult first = await fixture.Service.TransitionAsync(request);
        RequirementOperationResult duplicate = await fixture.Service.TransitionAsync(request);

        Assert.True(first.Succeeded);
        Assert.Equal(RequirementOperationFailure.Conflict, duplicate.Failure);
    }

    [Fact]
    public async Task ForgedStatusEnumIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync();

        RequirementOperationResult result = await fixture.Service.TransitionAsync(
            new TransitionRequirementRequest
            {
                ProjectId = fixture.Project.Id,
                RequirementId = requirement.Id,
                ExpectedStatus = requirement.Status,
                TargetStatus = (RequirementStatus)999,
                ActorUserId = fixture.ProjectManager.Id
            });

        Assert.Equal(RequirementOperationFailure.Validation, result.Failure);
    }

    [Theory]
    [InlineData(RequirementStatus.Draft, true)]
    [InlineData(RequirementStatus.Rejected, true)]
    [InlineData(RequirementStatus.UnderReview, false)]
    [InlineData(RequirementStatus.Approved, false)]
    [InlineData(RequirementStatus.Deprecated, false)]
    public async Task ContentEditHonorsRequirementState(
        RequirementStatus status,
        bool allowed)
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync(status);

        RequirementOperationResult result = await fixture.Service.UpdateAsync(
            fixture.UpdateRequest(requirement, fixture.ProjectManager.Id, "Updated title"));

        Assert.Equal(allowed, result.Succeeded);
        if (!allowed)
            Assert.Equal(RequirementOperationFailure.ReadOnly, result.Failure);
    }

    [Theory]
    [InlineData("scrum")]
    [InlineData("developer")]
    [InlineData("qa")]
    public async Task ViewOnlyRolesCannotEditRequirement(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync();

        RequirementOperationResult result = await fixture.Service.UpdateAsync(
            fixture.UpdateRequest(requirement, fixture.Actor(actor).Id, "Blocked"));

        Assert.Equal(RequirementOperationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task SameProjectDependencyCanBeAddedAndRemoved()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement source = await fixture.SeedRequirementAsync(identifier: "FR-001");
        Requirement target = await fixture.SeedRequirementAsync(identifier: "FR-002");

        RequirementOperationResult added = await fixture.Service.AddDependencyAsync(
            fixture.Dependency(source, target, fixture.ProjectManager.Id));
        RequirementOperationResult removed = await fixture.Service.RemoveDependencyAsync(
            new RemoveRequirementDependencyRequest
            {
                ProjectId = fixture.Project.Id,
                RequirementId = source.Id,
                DependencyId = added.LinkId!.Value,
                ActorUserId = fixture.ProjectManager.Id
            });

        Assert.True(added.Succeeded);
        Assert.True(removed.Succeeded);
        Assert.Empty(fixture.Context.RequirementDependencies);
    }

    [Fact]
    public async Task SelfDuplicateAndCrossProjectDependenciesAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement source = await fixture.SeedRequirementAsync(identifier: "FR-001");
        Requirement target = await fixture.SeedRequirementAsync(identifier: "FR-002");
        Requirement foreign = await fixture.SeedRequirementAsync(
            identifier: "FR-001", project: fixture.ForeignProject);

        RequirementOperationResult self = await fixture.Service.AddDependencyAsync(
            fixture.Dependency(source, source, fixture.ProjectManager.Id));
        RequirementOperationResult first = await fixture.Service.AddDependencyAsync(
            fixture.Dependency(source, target, fixture.ProjectManager.Id));
        RequirementOperationResult duplicate = await fixture.Service.AddDependencyAsync(
            fixture.Dependency(source, target, fixture.ProjectManager.Id));
        RequirementOperationResult crossProject = await fixture.Service.AddDependencyAsync(
            fixture.Dependency(source, foreign, fixture.ProjectManager.Id));

        Assert.Equal(RequirementOperationFailure.Validation, self.Failure);
        Assert.True(first.Succeeded);
        Assert.Equal(RequirementOperationFailure.Conflict, duplicate.Failure);
        Assert.Equal(RequirementOperationFailure.Validation, crossProject.Failure);
    }

    [Fact]
    public async Task DirectDependencyCycleIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement first = await fixture.SeedRequirementAsync(identifier: "FR-001");
        Requirement second = await fixture.SeedRequirementAsync(identifier: "FR-002");
        await fixture.Service.AddDependencyAsync(
            fixture.Dependency(first, second, fixture.ProjectManager.Id));

        RequirementOperationResult result = await fixture.Service.AddDependencyAsync(
            fixture.Dependency(second, first, fixture.ProjectManager.Id));

        Assert.Equal(RequirementOperationFailure.Validation, result.Failure);
    }

    [Fact]
    public async Task MultiLevelDependencyCycleIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement first = await fixture.SeedRequirementAsync(identifier: "FR-001");
        Requirement second = await fixture.SeedRequirementAsync(identifier: "FR-002");
        Requirement third = await fixture.SeedRequirementAsync(identifier: "FR-003");
        await fixture.Service.AddDependencyAsync(
            fixture.Dependency(first, second, fixture.ProjectManager.Id));
        await fixture.Service.AddDependencyAsync(
            fixture.Dependency(second, third, fixture.ProjectManager.Id));

        RequirementOperationResult result = await fixture.Service.AddDependencyAsync(
            fixture.Dependency(third, first, fixture.ProjectManager.Id));

        Assert.Equal(RequirementOperationFailure.Validation, result.Failure);
    }

    [Theory]
    [InlineData("scrum")]
    [InlineData("developer")]
    [InlineData("qa")]
    public async Task ViewOnlyRolesCannotManageDependencies(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement source = await fixture.SeedRequirementAsync(identifier: "FR-001");
        Requirement target = await fixture.SeedRequirementAsync(identifier: "FR-002");

        RequirementOperationResult result = await fixture.Service.AddDependencyAsync(
            fixture.Dependency(source, target, fixture.Actor(actor).Id));

        Assert.Equal(RequirementOperationFailure.Forbidden, result.Failure);
    }

    [Fact]
    public async Task ValidTraceCanBeAddedAndRemoved()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync();

        RequirementOperationResult added = await fixture.Service.AddTraceAsync(
            fixture.Trace(requirement, fixture.ProjectManager.Id));
        RequirementOperationResult removed = await fixture.Service.RemoveTraceAsync(
            new RemoveRequirementTraceRequest
            {
                ProjectId = fixture.Project.Id,
                RequirementId = requirement.Id,
                TraceId = added.LinkId!.Value,
                ActorUserId = fixture.ProjectManager.Id
            });

        Assert.True(added.Succeeded);
        Assert.True(removed.Succeeded);
        Assert.Empty(fixture.Context.RequirementTraces);
    }

    [Fact]
    public async Task DetailsAndTraceabilityReturnOnlyRealProjectLinks()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement source = await fixture.SeedRequirementAsync(identifier: "FR-001");
        Requirement target = await fixture.SeedRequirementAsync(identifier: "FR-002");
        await fixture.Service.AddDependencyAsync(
            fixture.Dependency(source, target, fixture.ProjectManager.Id));
        await fixture.Service.AddTraceAsync(
            fixture.Trace(source, fixture.ProjectManager.Id));

        RequirementDetails? details = await fixture.Service.GetByIdAsync(
            fixture.Project.Id, source.Id);
        IReadOnlyList<RequirementTraceabilityItem> traceability =
            await fixture.Service.GetTraceabilityAsync(fixture.Project.Id);

        Assert.NotNull(details);
        Assert.Single(details.Dependencies);
        Assert.Single(details.Traces);
        RequirementTraceabilityItem row = Assert.Single(
            traceability, item => item.RequirementId == source.Id);
        Assert.Single(row.Dependencies);
        Assert.Single(row.Traces);
    }

    [Fact]
    public async Task DuplicateAndInvalidTraceAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync();
        await fixture.Service.AddTraceAsync(
            fixture.Trace(requirement, fixture.ProjectManager.Id));

        RequirementOperationResult duplicate = await fixture.Service.AddTraceAsync(
            fixture.Trace(requirement, fixture.ProjectManager.Id));
        RequirementOperationResult invalidStage = await fixture.Service.AddTraceAsync(
            fixture.Trace(
                requirement,
                fixture.ProjectManager.Id,
                (RequirementTraceStage)999,
                "BAD"));

        Assert.Equal(RequirementOperationFailure.Conflict, duplicate.Failure);
        Assert.Equal(RequirementOperationFailure.Validation, invalidStage.Failure);
    }

    [Fact]
    public async Task CrossProjectAndUnauthorizedTraceMutationsAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement requirement = await fixture.SeedRequirementAsync();
        Requirement foreign = await fixture.SeedRequirementAsync(
            identifier: "FR-001", project: fixture.ForeignProject);

        RequirementOperationResult crossProject = await fixture.Service.AddTraceAsync(
            new AddRequirementTraceRequest
            {
                ProjectId = fixture.Project.Id,
                RequirementId = foreign.Id,
                Stage = RequirementTraceStage.Design,
                ReferenceCode = "DES-1",
                ActorUserId = fixture.ProjectManager.Id
            });
        RequirementOperationResult unauthorized = await fixture.Service.AddTraceAsync(
            fixture.Trace(requirement, fixture.Developer.Id));

        Assert.Equal(RequirementOperationFailure.NotFound, crossProject.Failure);
        Assert.Equal(RequirementOperationFailure.Forbidden, unauthorized.Failure);
    }

    [Fact]
    public async Task UnauthorizedRoleCannotRemoveTraceOrDependency()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement source = await fixture.SeedRequirementAsync(identifier: "FR-001");
        Requirement target = await fixture.SeedRequirementAsync(identifier: "FR-002");
        RequirementOperationResult dependency = await fixture.Service.AddDependencyAsync(
            fixture.Dependency(source, target, fixture.ProjectManager.Id));
        RequirementOperationResult trace = await fixture.Service.AddTraceAsync(
            fixture.Trace(source, fixture.ProjectManager.Id));

        RequirementOperationResult removeDependency = await fixture.Service.RemoveDependencyAsync(
            new RemoveRequirementDependencyRequest
            {
                ProjectId = fixture.Project.Id,
                RequirementId = source.Id,
                DependencyId = dependency.LinkId!.Value,
                ActorUserId = fixture.ScrumMaster.Id
            });
        RequirementOperationResult removeTrace = await fixture.Service.RemoveTraceAsync(
            new RemoveRequirementTraceRequest
            {
                ProjectId = fixture.Project.Id,
                RequirementId = source.Id,
                TraceId = trace.LinkId!.Value,
                ActorUserId = fixture.QaTester.Id
            });

        Assert.Equal(RequirementOperationFailure.Forbidden, removeDependency.Failure);
        Assert.Equal(RequirementOperationFailure.Forbidden, removeTrace.Failure);
    }

    [Fact]
    public async Task ForgedProjectAndRequirementIdsAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement foreign = await fixture.SeedRequirementAsync(
            identifier: "FR-001", project: fixture.ForeignProject);

        RequirementOperationResult result = await fixture.Service.UpdateAsync(
            new UpdateRequirementRequest
            {
                ProjectId = fixture.Project.Id,
                RequirementId = foreign.Id,
                ExpectedStatus = foreign.Status,
                Title = "Forged",
                Description = foreign.Description,
                Priority = foreign.Priority,
                ActorUserId = fixture.ProjectManager.Id
            });

        Assert.Equal(RequirementOperationFailure.NotFound, result.Failure);
    }

    [Fact]
    public async Task CrossProjectRequirementCannotBeOpened()
    {
        await using var fixture = await Fixture.CreateAsync();
        Requirement foreign = await fixture.SeedRequirementAsync(
            identifier: "FR-001", project: fixture.ForeignProject);
        RequirementsController controller = fixture.CreateController(fixture.ProjectManager);

        IActionResult result = await controller.Details(
            fixture.Project.Id, foreign.Id, default);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task InvalidEnumsAndOverlengthContentAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        CreateRequirementRequest valid = fixture.CreateRequest(fixture.ProjectManager.Id);

        RequirementOperationResult invalidType = await fixture.Service.CreateAsync(
            new CreateRequirementRequest
            {
                ProjectId = valid.ProjectId,
                Title = valid.Title,
                Description = valid.Description,
                Type = (RequirementType)999,
                Priority = valid.Priority,
                ActorUserId = valid.ActorUserId
            });
        RequirementOperationResult invalidPriority = await fixture.Service.CreateAsync(
            new CreateRequirementRequest
            {
                ProjectId = valid.ProjectId,
                Title = valid.Title,
                Description = valid.Description,
                Type = valid.Type,
                Priority = (PriorityLevel)999,
                ActorUserId = valid.ActorUserId
            });
        RequirementOperationResult overlength = await fixture.Service.CreateAsync(
            new CreateRequirementRequest
            {
                ProjectId = valid.ProjectId,
                Title = new string('x', 201),
                Description = valid.Description,
                Type = valid.Type,
                Priority = valid.Priority,
                ActorUserId = valid.ActorUserId
            });

        Assert.Equal(RequirementOperationFailure.Validation, invalidType.Failure);
        Assert.Equal(RequirementOperationFailure.Validation, invalidPriority.Failure);
        Assert.Equal(RequirementOperationFailure.Validation, overlength.Failure);
    }

    [Fact]
    public async Task RequirementContentIsRenderedWithRazorEncoding()
    {
        await using var fixture = await Fixture.CreateAsync();
        const string payload = "<script>alert('xss')</script>";
        CreateRequirementRequest source = fixture.CreateRequest(fixture.ProjectManager.Id);

        RequirementOperationResult result = await fixture.Service.CreateAsync(
            new CreateRequirementRequest
            {
                ProjectId = source.ProjectId,
                Title = payload,
                Description = payload,
                Type = source.Type,
                Priority = source.Priority,
                Rationale = payload,
                ActorUserId = source.ActorUserId
            });
        string view = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "Planora.Web", "Views", "Requirements", "Details.cshtml"));

        Assert.True(result.Succeeded);
        Assert.DoesNotContain("Html.Raw", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("<p>@line</p>", view, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryRequirementMutationWritesExpectedAuditEntry()
    {
        await using var fixture = await Fixture.CreateAsync();
        RequirementOperationResult created = await fixture.Service.CreateAsync(
            fixture.CreateRequest(fixture.ProjectManager.Id));
        Requirement requirement = await fixture.Context.Requirements
            .SingleAsync(item => item.Id == created.RequirementId);
        Requirement dependencyTarget = await fixture.SeedRequirementAsync(identifier: "FR-002");
        await fixture.Service.UpdateAsync(
            fixture.UpdateRequest(requirement, fixture.ProjectManager.Id, "Edited"));
        await fixture.Service.TransitionAsync(
            fixture.Transition(requirement, fixture.ProjectManager.Id, RequirementStatus.UnderReview));
        RequirementOperationResult dependency = await fixture.Service.AddDependencyAsync(
            fixture.Dependency(requirement, dependencyTarget, fixture.ProjectManager.Id));
        RequirementOperationResult trace = await fixture.Service.AddTraceAsync(
            fixture.Trace(requirement, fixture.ProjectManager.Id));
        await fixture.Service.RemoveDependencyAsync(new RemoveRequirementDependencyRequest
        {
            ProjectId = fixture.Project.Id,
            RequirementId = requirement.Id,
            DependencyId = dependency.LinkId!.Value,
            ActorUserId = fixture.ProjectManager.Id
        });
        await fixture.Service.RemoveTraceAsync(new RemoveRequirementTraceRequest
        {
            ProjectId = fixture.Project.Id,
            RequirementId = requirement.Id,
            TraceId = trace.LinkId!.Value,
            ActorUserId = fixture.ProjectManager.Id
        });

        string[] actions = fixture.Context.ActivityLogs.Select(log => log.Action).ToArray();
        Assert.Contains("RequirementCreated", actions);
        Assert.Contains("RequirementEdited", actions);
        Assert.Contains("RequirementStatusChanged", actions);
        Assert.Contains("RequirementDependencyAdded", actions);
        Assert.Contains("RequirementDependencyRemoved", actions);
        Assert.Contains("TraceabilityLinkAdded", actions);
        Assert.Contains("TraceabilityLinkRemoved", actions);
        Assert.DoesNotContain(fixture.Context.ActivityLogs,
            log => log.NewValues != null && log.NewValues.Contains(requirement.Description));
    }

    [Fact]
    public void MutationEndpointsRequireAntiforgeryAndDeleteIsNotImplemented()
    {
        MethodInfo[] posts = typeof(RequirementsController).GetMethods()
            .Where(method => method.GetCustomAttribute<HttpPostAttribute>() is not null)
            .ToArray();

        Assert.Equal(
            ["AddDependency", "AddTrace", "Create", "Edit", "RemoveDependency", "RemoveTrace", "Transition"],
            posts.Select(method => method.Name).OrderBy(name => name).ToArray());
        Assert.All(posts, method => Assert.NotNull(
            method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>()));
        Assert.DoesNotContain(typeof(RequirementsController).GetMethods(),
            method => method.Name.Contains("Delete", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ProjectNavigationScopesRequirementsToVModelProjects()
    {
        string view = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "src", "Planora.Web", "Views", "Projects", "Details.cshtml"));
        int condition = view.IndexOf(
            "@if (Model.Methodology.ToString() == \"VModel\")",
            StringComparison.Ordinal);
        int route = view.IndexOf(
            "asp-controller=\"Requirements\"",
            StringComparison.Ordinal);

        Assert.True(condition >= 0);
        Assert.True(route > condition);
    }

    [Fact]
    public void RequirementViewsContainDynamicTypeFormAndIeeeLabel()
    {
        string viewsDirectory = Path.Combine(
            RepositoryRoot(), "src", "Planora.Web", "Views", "Requirements");
        string create = File.ReadAllText(Path.Combine(viewsDirectory, "Create.cshtml"));
        string edit = File.ReadAllText(Path.Combine(viewsDirectory, "Edit.cshtml"));
        string details = File.ReadAllText(Path.Combine(viewsDirectory, "Details.cshtml"));
        string traceability = File.ReadAllText(Path.Combine(viewsDirectory, "Traceability.cshtml"));

        Assert.Contains("data-requirement-type", create);
        Assert.Contains("data-type-section=\"functional\"", create);
        Assert.Contains("data-type-section=\"nonfunctional\"", create);
        Assert.Contains("requirement-form.js", create);
        Assert.DoesNotContain("asp-for=\"Type\"", edit);
        Assert.All(
            new[] { create, edit, details, traceability },
            view => Assert.Contains("IEEE SRS standards", view));
    }

    private static string RepositoryRoot([CallerFilePath] string path = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));

    private static bool IsApprovedTransition(
        RequirementStatus source,
        RequirementStatus target) => source switch
        {
            RequirementStatus.Draft => target == RequirementStatus.UnderReview,
            RequirementStatus.UnderReview =>
                target is RequirementStatus.Approved or RequirementStatus.Rejected,
            RequirementStatus.Rejected => target == RequirementStatus.Draft,
            RequirementStatus.Approved => target == RequirementStatus.Deprecated,
            _ => false
        };

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        public ApplicationDbContext Context { get; }
        public RequirementService Service { get; }
        public Project Project { get; }
        public Project ForeignProject { get; }
        public Project ScrumProject { get; }
        public ApplicationUser Admin { get; }
        public ApplicationUser ProjectManager { get; }
        public ApplicationUser ScrumMaster { get; }
        public ApplicationUser Developer { get; }
        public ApplicationUser QaTester { get; }
        public ApplicationUser Outsider { get; }

        private Fixture(ServiceProvider provider)
        {
            _provider = provider;
            Context = provider.GetRequiredService<ApplicationDbContext>();
            Service = provider.GetRequiredService<RequirementService>();
            UserManager<ApplicationUser> users = provider.GetRequiredService<UserManager<ApplicationUser>>();
            RoleManager<IdentityRole> roles = provider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (string role in SystemRoles.All)
                Assert.True(roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult().Succeeded);

            Admin = CreateUser(users, "admin", SystemRoles.Admin);
            ProjectManager = CreateUser(users, "manager", SystemRoles.ProjectManager);
            ScrumMaster = CreateUser(users, "scrum", SystemRoles.ScrumMaster);
            Developer = CreateUser(users, "developer", SystemRoles.Developer);
            QaTester = CreateUser(users, "qa", SystemRoles.QaTester);
            Outsider = CreateUser(users, "outsider", SystemRoles.Developer);

            Project = new Project
            {
                Name = "V-Model project",
                Methodology = ProjectMethodology.VModel,
                Status = ProjectStatus.Active
            };
            ForeignProject = new Project
            {
                Name = "Foreign V-Model project",
                Methodology = ProjectMethodology.VModel,
                Status = ProjectStatus.Active
            };
            ScrumProject = new Project
            {
                Name = "Scrum project",
                Methodology = ProjectMethodology.Scrum,
                Status = ProjectStatus.Active
            };
            Context.AddRange(
                Project,
                ForeignProject,
                ScrumProject,
                new ProjectMember { Project = Project, UserId = ProjectManager.Id, Role = ProjectMemberRole.ProjectManager },
                new ProjectMember { Project = Project, UserId = ScrumMaster.Id, Role = ProjectMemberRole.ScrumMaster },
                new ProjectMember { Project = Project, UserId = Developer.Id, Role = ProjectMemberRole.Developer },
                new ProjectMember { Project = Project, UserId = QaTester.Id, Role = ProjectMemberRole.QaTester });
            Context.SaveChanges();
        }

        public static Task<Fixture> CreateAsync()
        {
            string databaseName = Guid.NewGuid().ToString();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseInMemoryDatabase(databaseName));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton<IClock, TestClock>();
            services.AddScoped<RequirementService>();
            return Task.FromResult(new Fixture(services.BuildServiceProvider()));
        }

        public IServiceScope CreateScope() => _provider.CreateScope();

        public CreateRequirementRequest CreateRequest(
            string actorUserId,
            RequirementType type = RequirementType.Functional,
            string title = "User authentication",
            int? projectId = null) => new()
            {
                ProjectId = projectId ?? Project.Id,
                Title = title,
                Description = "The system shall authenticate registered users.",
                Type = type,
                Priority = PriorityLevel.High,
                Rationale = "Protect access to project information.",
                NfrCategory = type == RequirementType.NonFunctional
                    ? Planora.Domain.Enums.NfrCategory.Security
                    : null,
                ActorUserId = actorUserId
            };

        public CreateRequirementRequest CreateNfrRequest(
            string actorUserId,
            IReadOnlyList<int>? relatedFunctionalRequirementIds = null,
            NfrCategory? category = NfrCategory.Security,
            int? projectId = null) => new()
            {
                ProjectId = projectId ?? Project.Id,
                Description = "The system shall protect account data at rest.",
                Type = RequirementType.NonFunctional,
                Priority = PriorityLevel.High,
                Rationale = "Protect confidential user information.",
                NfrCategory = category,
                RelatedFunctionalRequirementIds = relatedFunctionalRequirementIds ?? [],
                ActorUserId = actorUserId
            };

        public UpdateRequirementRequest UpdateNfrRequest(
            Requirement requirement,
            IReadOnlyList<int> relatedFunctionalRequirementIds,
            NfrCategory? category = NfrCategory.Security,
            string description = "Updated non-functional requirement.") => new()
            {
                ProjectId = requirement.ProjectId,
                RequirementId = requirement.Id,
                ExpectedStatus = requirement.Status,
                Description = description,
                Priority = requirement.Priority,
                Rationale = requirement.Rationale,
                NfrCategory = category,
                RelatedFunctionalRequirementIds = relatedFunctionalRequirementIds,
                ActorUserId = ProjectManager.Id
            };

        public async Task<Requirement> SeedRequirementAsync(
            RequirementStatus status = RequirementStatus.Draft,
            RequirementType type = RequirementType.Functional,
            string identifier = "FR-001",
            Project? project = null)
        {
            var requirement = new Requirement
            {
                Project = project ?? Project,
                Identifier = identifier,
                Title = $"Requirement {identifier}",
                Description = "Requirement description",
                Type = type,
                NfrCategory = type == RequirementType.NonFunctional
                    ? Planora.Domain.Enums.NfrCategory.Security
                    : null,
                Priority = PriorityLevel.Medium,
                Status = status,
                CreatedByUserId = ProjectManager.Id,
                CreatedAt = new TestClock().UtcNow
            };
            Context.Add(requirement);
            await Context.SaveChangesAsync();
            return requirement;
        }

        public UpdateRequirementRequest UpdateRequest(
            Requirement requirement,
            string actorUserId,
            string title) => new()
            {
                ProjectId = requirement.ProjectId,
                RequirementId = requirement.Id,
                ExpectedStatus = requirement.Status,
                Title = title,
                Description = requirement.Description,
                Priority = requirement.Priority,
                Rationale = requirement.Rationale,
                Preconditions = requirement.Preconditions,
                ExceptionScenario = requirement.ExceptionScenario,
                NfrCategory = requirement.NfrCategory,
                ActorUserId = actorUserId
            };

        public TransitionRequirementRequest Transition(
            Requirement requirement,
            string actorUserId,
            RequirementStatus target) => new()
            {
                ProjectId = requirement.ProjectId,
                RequirementId = requirement.Id,
                ExpectedStatus = requirement.Status,
                TargetStatus = target,
                ActorUserId = actorUserId
            };

        public AddRequirementDependencyRequest Dependency(
            Requirement source,
            Requirement target,
            string actorUserId) => new()
            {
                ProjectId = source.ProjectId,
                RequirementId = source.Id,
                DependsOnRequirementId = target.Id,
                ActorUserId = actorUserId
            };

        public AddRequirementTraceRequest Trace(
            Requirement requirement,
            string actorUserId,
            RequirementTraceStage stage = RequirementTraceStage.Design,
            string code = "DES-001") => new()
            {
                ProjectId = requirement.ProjectId,
                RequirementId = requirement.Id,
                Stage = stage,
                ReferenceCode = code,
                Description = "Architecture document reference",
                ActorUserId = actorUserId
            };

        public ApplicationUser Actor(string name) => name switch
        {
            "admin" => Admin,
            "manager" => ProjectManager,
            "scrum" => ScrumMaster,
            "qa" => QaTester,
            _ => Developer
        };

        public RequirementsController CreateController(ApplicationUser user)
        {
            var identity = new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, user.Id)], "Test");
            foreach (string role in Context.UserRoles
                .Where(item => item.UserId == user.Id)
                .Join(Context.Roles, item => item.RoleId, role => role.Id,
                    (_, role) => role.Name!)
                .ToList())
                identity.AddClaim(new Claim(ClaimTypes.Role, role));

            return new RequirementsController(
                Service,
                new ProjectService(Context, new TestClock()),
                new ProjectAccessService(Context),
                NullLogger<RequirementsController>.Instance)
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

        private static ApplicationUser CreateUser(
            UserManager<ApplicationUser> users,
            string name,
            string role)
        {
            var user = new ApplicationUser
            {
                UserName = $"{name}@requirements.test",
                Email = $"{name}@requirements.test",
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

    private sealed class MemoryTempDataProvider : ITempDataProvider
    {
        private Dictionary<string, object> _values = [];

        public IDictionary<string, object> LoadTempData(
            Microsoft.AspNetCore.Http.HttpContext context) =>
            new Dictionary<string, object>(_values);

        public void SaveTempData(
            Microsoft.AspNetCore.Http.HttpContext context,
            IDictionary<string, object> values)
        {
            _values = new Dictionary<string, object>(values);
        }
    }
}
