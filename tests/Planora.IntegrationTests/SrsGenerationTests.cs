using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Planora.Application.Abstractions.Ai;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Requirements;
using Planora.Application.Abstractions.Srs;
using Planora.Application.Common.Ai;
using Planora.Application.Common.Security;
using Planora.Application.Common.Srs;
using Planora.Domain.Entities;
using Planora.Domain.Enums;
using Planora.Infrastructure.Gemini;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Persistence;
using Planora.Infrastructure.Requirements;
using Planora.Web.Controllers;
using Planora.Web.Documents;
using Planora.Web.ViewModels.Srs;
using QuestPDF.Infrastructure;

namespace Planora.IntegrationTests;

public sealed class SrsGenerationTests
{
    [Fact]
    public async Task DraftTokenCanBeClaimedOnlyOnceUnderConcurrency()
    {
        var clock = new Clock
        {
            UtcNow = new DateTime(2026, 9, 19, 12, 0, 0, DateTimeKind.Utc)
        };
        var cache = new InMemorySrsDraftCache(clock);
        string token = cache.Store(new SrsDraft
        {
            ProjectId = 42,
            ProjectName = "Planora",
            OwnerUserId = "manager",
            Content = new SrsContent(),
            QualityScore = 80,
            QualityLevel = AiInputQualityLevel.Good,
            GeneratedByUserId = "manager",
            GeneratedAt = clock.UtcNow
        });

        SrsDraft?[] claims = await Task.WhenAll(Enumerable.Range(0, 12)
            .Select(_ => Task.Run(() => cache.Take("manager", 42, token))));

        Assert.Single(claims, claim => claim is not null);
    }

    [Theory]
    [InlineData("admin", true)]
    [InlineData("manager", true)]
    [InlineData("developer", false)]
    [InlineData("qa", false)]
    [InlineData("scrum", false)]
    public async Task GenerationAuthorizationMatchesApprovedPolicy(string actor, bool allowed)
    {
        await using var fixture = await Fixture.CreateAsync();
        SrsGenerationResult result = await fixture.Srs.GenerateAsync(fixture.Generate(fixture.User(actor).Id));
        Assert.Equal(allowed, result.Succeeded);
        if (!allowed) Assert.Equal(SrsOperationFailure.Forbidden, result.Failure);
    }

    [Theory]
    [InlineData("manager")]
    [InlineData("developer")]
    [InlineData("qa")]
    [InlineData("scrum")]
    public async Task MatchingProjectMembersCanViewSavedSrs(string actor)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.AddSavedDocumentAsync(fixture.Admin.Id, fixture.Project.Id);
        SrsListResult result = await fixture.Srs.GetDocumentsAsync(fixture.Project.Id, fixture.User(actor).Id);
        Assert.True(result.Succeeded, result.Message);
        Assert.Single(result.Documents);
        Assert.Equal(actor == "manager", result.CanManage);
    }

    [Fact]
    public async Task NonMemberAndCrossProjectDocumentAccessAreDenied()
    {
        await using var fixture = await Fixture.CreateAsync();
        int documentId = await fixture.AddSavedDocumentAsync(fixture.Admin.Id, fixture.Project.Id);
        Assert.Equal(SrsOperationFailure.Forbidden,
            (await fixture.Srs.GetDocumentsAsync(fixture.Project.Id, fixture.Outsider.Id)).Failure);
        Assert.Equal(SrsOperationFailure.NotFound,
            (await fixture.Srs.GetDocumentAsync(fixture.ForeignProject.Id, documentId, fixture.Admin.Id)).Failure);
    }

    [Fact]
    public async Task ArchivedProjectIsReadOnlyButSavedDocumentsRemainAvailableForExport()
    {
        await using var fixture = await Fixture.CreateAsync();
        int documentId = await fixture.AddSavedDocumentAsync(fixture.Admin.Id, fixture.ArchivedProject.Id);
        Assert.Equal(SrsOperationFailure.ReadOnly,
            (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Admin.Id, fixture.ArchivedProject.Id))).Failure);
        Assert.True((await fixture.Srs.GetDocumentAsync(
            fixture.ArchivedProject.Id, documentId, fixture.Admin.Id)).Succeeded);
        Assert.True((await fixture.Srs.RecordExportAsync(
            fixture.ArchivedProject.Id, documentId, fixture.Admin.Id, "TXT")).Succeeded);
    }

    [Fact]
    public async Task ScrumProjectGenerationIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        SrsGenerationResult result = await fixture.Srs.GenerateAsync(
            fixture.Generate(fixture.Admin.Id, fixture.ScrumProject.Id));
        Assert.Equal(SrsOperationFailure.Forbidden, result.Failure);
        Assert.Equal(0, fixture.Gemini.CallCount);
    }

    [Fact]
    public async Task GenerationRequiresAtLeastOneApprovedRequirement()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Fr.Status = RequirementStatus.Draft;
        fixture.Nfr.Status = RequirementStatus.Rejected;
        await fixture.Context.SaveChangesAsync();
        SrsGenerationResult result = await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id));
        Assert.Equal(SrsOperationFailure.Validation, result.Failure);
        Assert.Contains("Approve at least one", result.Message);
        Assert.Equal(0, fixture.Gemini.CallCount);
    }

    [Fact]
    public async Task InsufficientQualityBlocksSrsGeminiCall()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Gemini.QualityResult = GeminiClientResult.Success(QualityJson(60, "NeedsImprovement", false));
        SrsGenerationResult result = await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id));
        Assert.Equal(SrsOperationFailure.Validation, result.Failure);
        Assert.NotNull(result.QualityAnalysis);
        Assert.Equal(1, fixture.Gemini.QualityCallCount);
        Assert.Equal(0, fixture.Gemini.SrsCallCount);
    }

    [Fact]
    public async Task PassingExactInputUsesExistingQualityApproval()
    {
        await using var fixture = await Fixture.CreateAsync();
        const string context = "Only verified customers may submit requests.";
        Assert.True((await fixture.Quality.AnalyzeInputQualityAsync(
            fixture.Analyze(fixture.Manager.Id, context))).CanGenerate);
        SrsGenerationResult result = await fixture.Srs.GenerateAsync(
            fixture.Generate(fixture.Manager.Id, context: context));
        Assert.True(result.Succeeded, result.Message);
        Assert.Equal(1, fixture.Gemini.QualityCallCount);
        Assert.Equal(1, fixture.Gemini.SrsCallCount);
    }

    [Fact]
    public async Task ChangedInputInvalidatesQualityApprovalBeforeSrsGeneration()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True((await fixture.Quality.AnalyzeInputQualityAsync(
            fixture.Analyze(fixture.Manager.Id, "Original context."))).CanGenerate);
        Assert.True((await fixture.Srs.GenerateAsync(
            fixture.Generate(fixture.Manager.Id, context: "Changed context."))).Succeeded);
        Assert.Equal(2, fixture.Gemini.QualityCallCount);
        Assert.Equal(1, fixture.Gemini.SrsCallCount);
    }

    [Fact]
    public async Task ValidGenerationCreatesOwnerBoundTemporaryDraftWithoutDatabaseDocument()
    {
        await using var fixture = await Fixture.CreateAsync();
        SrsGenerationResult result = await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id));
        Assert.True(result.Succeeded, result.Message);
        Assert.NotNull(result.Draft);
        Assert.Equal(32, result.Draft.Token.Length);
        Assert.Empty(fixture.Context.SrsDocuments);
        Assert.Equal(2, fixture.Gemini.CallCount);
        Assert.Contains(fixture.Context.ActivityLogs, log => log.Action == "SrsGenerated");
        Assert.DoesNotContain(fixture.Context.ActivityLogs,
            log => (log.NewValues ?? string.Empty).Contains("The system shall", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DraftOwnerCanReviewButAnotherManagerCannotUseToken()
    {
        await using var fixture = await Fixture.CreateAsync();
        SrsDraft draft = (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Draft!;
        Assert.True((await fixture.Srs.GetDraftAsync(
            fixture.Project.Id, draft.Token, fixture.Manager.Id)).Succeeded);
        SrsDraftResult other = await fixture.Srs.GetDraftAsync(
            fixture.Project.Id, draft.Token, fixture.Admin.Id);
        Assert.Equal(SrsOperationFailure.Expired, other.Failure);
    }

    [Fact]
    public async Task ExpiredDraftCannotBeReviewedOrSaved()
    {
        await using var fixture = await Fixture.CreateAsync();
        SrsDraft draft = (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Draft!;
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(21);
        Assert.Equal(SrsOperationFailure.Expired,
            (await fixture.Srs.GetDraftAsync(fixture.Project.Id, draft.Token, fixture.Manager.Id)).Failure);
        Assert.Equal(SrsOperationFailure.Expired,
            (await fixture.Srs.SaveAsync(new SaveSrsRequest
            {
                ProjectId = fixture.Project.Id, ActorUserId = fixture.Manager.Id,
                DraftToken = draft.Token, Content = draft.Content
            })).Failure);
    }

    [Fact]
    public async Task DeveloperQaAndScrumMasterCannotSaveManagerDraft()
    {
        await using var fixture = await Fixture.CreateAsync();
        SrsDraft draft = (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Draft!;
        foreach (ApplicationUser user in new[] { fixture.Developer, fixture.Qa, fixture.ScrumMaster })
        {
            SrsSaveResult result = await fixture.Srs.SaveAsync(new SaveSrsRequest
            {
                ProjectId = fixture.Project.Id, DraftToken = draft.Token,
                ActorUserId = user.Id, Content = draft.Content
            });
            Assert.Equal(SrsOperationFailure.Forbidden, result.Failure);
        }
        Assert.Empty(fixture.Context.SrsDocuments);
    }

    [Fact]
    public async Task ProjectArchivedAfterGenerationCannotSaveDraft()
    {
        await using var fixture = await Fixture.CreateAsync();
        SrsDraft draft = (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Draft!;
        fixture.Project.Status = ProjectStatus.Archived;
        await fixture.Context.SaveChangesAsync();
        SrsSaveResult result = await fixture.Srs.SaveAsync(new SaveSrsRequest
        {
            ProjectId = fixture.Project.Id, DraftToken = draft.Token,
            ActorUserId = fixture.Manager.Id, Content = draft.Content
        });
        Assert.Equal(SrsOperationFailure.ReadOnly, result.Failure);
        Assert.Empty(fixture.Context.SrsDocuments);
    }

    [Fact]
    public async Task StaleQualityApprovalIsReanalyzedForSrs()
    {
        await using var fixture = await Fixture.CreateAsync();
        Assert.True((await fixture.Quality.AnalyzeInputQualityAsync(
            fixture.Analyze(fixture.Manager.Id))).CanGenerate);
        fixture.Clock.UtcNow = fixture.Clock.UtcNow.AddMinutes(11);
        Assert.True((await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Succeeded);
        Assert.Equal(2, fixture.Gemini.QualityCallCount);
        Assert.Equal(1, fixture.Gemini.SrsCallCount);
    }

    [Theory]
    [InlineData(GeminiClientFailure.Timeout)]
    [InlineData(GeminiClientFailure.RateLimited)]
    [InlineData(GeminiClientFailure.Authentication)]
    public async Task SrsProviderFailuresReturnControlledErrors(GeminiClientFailure failure)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Gemini.SrsResult = GeminiClientResult.Failed(failure, "SRS generation is temporarily unavailable.");
        SrsGenerationResult result = await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id));
        Assert.False(result.Succeeded);
        Assert.DoesNotContain("ApiKey", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(fixture.Context.SrsDocuments);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("")]
    public async Task MalformedOrEmptySrsResponseIsHandled(string response)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Gemini.SrsResult = GeminiClientResult.Success(response);
        SrsGenerationResult result = await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id));
        Assert.Equal(SrsOperationFailure.InvalidResponse, result.Failure);
        Assert.Empty(fixture.Context.SrsDocuments);
    }

    [Fact]
    public async Task MissingRequiredSectionIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Gemini.SrsResult = GeminiClientResult.Success(JsonSerializer.Serialize(new
        {
            documentTitle = "Incomplete",
            introduction = new { purpose = "Purpose", scope = "Scope", documentOverview = "Overview" }
        }));
        Assert.Equal(SrsOperationFailure.InvalidResponse,
            (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Failure);
    }

    [Theory]
    [InlineData("FR-001", "FR-999")]
    [InlineData("NFR-001", "NFR-999")]
    public async Task UnknownRequirementIdentifiersAreRejected(string known, string unknown)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Gemini.SrsResult = GeminiClientResult.Success(ValidSrsJson().Replace(known, unknown));
        Assert.Equal(SrsOperationFailure.InvalidResponse,
            (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Failure);
    }

    [Fact]
    public async Task DuplicateRequirementIdentifierIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Gemini.SrsResult = GeminiClientResult.Success(ValidSrsJson(duplicateFr: true));
        Assert.Equal(SrsOperationFailure.InvalidResponse,
            (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Failure);
    }

    [Fact]
    public async Task OversizedSrsResponseIsRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Gemini.SrsResult = GeminiClientResult.Success(new string('x', 500_001));
        Assert.Equal(SrsOperationFailure.InvalidResponse,
            (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Failure);
    }

    [Fact]
    public async Task GeminiUnavailableDoesNotPreventViewingSavedSrs()
    {
        await using var fixture = await Fixture.CreateAsync(configured: false);
        int id = await fixture.AddSavedDocumentAsync(fixture.Admin.Id, fixture.Project.Id);
        Assert.Equal(SrsOperationFailure.ConfigurationUnavailable,
            (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Admin.Id))).Failure);
        Assert.True((await fixture.Srs.GetDocumentAsync(
            fixture.Project.Id, id, fixture.Admin.Id)).Succeeded);
    }

    [Fact]
    public async Task ExplicitSavePersistsReviewedCanonicalSnapshotWithServerMetadata()
    {
        await using var fixture = await Fixture.CreateAsync();
        SrsDraft draft = (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Draft!;
        SrsContent edited = CopyContent(draft.Content, title: "Reviewed Project SRS", purpose: "Reviewed purpose.");
        SrsSaveResult saved = await fixture.Srs.SaveAsync(new SaveSrsRequest
        {
            ProjectId = fixture.Project.Id, DraftToken = draft.Token,
            ActorUserId = fixture.Manager.Id, Content = edited
        });
        Assert.True(saved.Succeeded, saved.Message);
        SrsDocument document = await fixture.Context.SrsDocuments.SingleAsync();
        Assert.Equal("Reviewed Project SRS", document.Title);
        Assert.Equal(fixture.Manager.Id, document.GeneratedByUserId);
        Assert.Equal(fixture.Manager.Id, document.SavedByUserId);
        Assert.DoesNotContain("Gemini", document.StructuredContentJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(fixture.Context.ActivityLogs, log => log.Action == "SrsSaved");
    }

    [Fact]
    public async Task RequirementIdentifiersCannotBeEditedBeforeSave()
    {
        await using var fixture = await Fixture.CreateAsync();
        SrsDraft draft = (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Draft!;
        SrsContent forged = CopyContent(draft.Content,
            functional: draft.Content.FunctionalRequirements.Select(item => new SrsFunctionalRequirement
            {
                Identifier = "FR-999", Name = item.Name, Priority = item.Priority,
                Description = item.Description, BusinessRationale = item.BusinessRationale,
                Preconditions = item.Preconditions, ExceptionScenario = item.ExceptionScenario
            }).ToList());
        SrsSaveResult result = await fixture.Srs.SaveAsync(new SaveSrsRequest
        {
            ProjectId = fixture.Project.Id, DraftToken = draft.Token,
            ActorUserId = fixture.Manager.Id, Content = forged
        });
        Assert.Equal(SrsOperationFailure.Validation, result.Failure);
        Assert.Empty(fixture.Context.SrsDocuments);
    }

    [Fact]
    public async Task NewSavedSrsDoesNotOverwriteOlderSnapshot()
    {
        await using var fixture = await Fixture.CreateAsync();
        for (int i = 1; i <= 2; i++)
        {
            SrsDraft draft = (await fixture.Srs.GenerateAsync(
                fixture.Generate(fixture.Manager.Id, context: $"Round {i}"))).Draft!;
            Assert.True((await fixture.Srs.SaveAsync(new SaveSrsRequest
            {
                ProjectId = fixture.Project.Id, DraftToken = draft.Token,
                ActorUserId = fixture.Manager.Id,
                Content = CopyContent(draft.Content, title: $"Snapshot {i}")
            })).Succeeded);
        }
        Assert.Equal(2, await fixture.Context.SrsDocuments.CountAsync());
        Assert.Equal(new[] { "Snapshot 2", "Snapshot 1" },
            (await fixture.Srs.GetDocumentsAsync(fixture.Project.Id, fixture.Manager.Id))
                .Documents.Select(item => item.Title));
    }

    [Fact]
    public async Task PreviewSaveAndExportsDoNotCallGeminiAgain()
    {
        await using var fixture = await Fixture.CreateAsync();
        SrsDraft draft = (await fixture.Srs.GenerateAsync(fixture.Generate(fixture.Manager.Id))).Draft!;
        Assert.Equal(2, fixture.Gemini.CallCount);
        Assert.True((await fixture.Srs.GetDraftAsync(
            fixture.Project.Id, draft.Token, fixture.Manager.Id)).Succeeded);
        SrsSaveResult save = await fixture.Srs.SaveAsync(new SaveSrsRequest
        {
            ProjectId = fixture.Project.Id, DraftToken = draft.Token,
            ActorUserId = fixture.Manager.Id, Content = draft.Content
        });
        Assert.Equal(2, fixture.Gemini.CallCount);
        SrsController controller = fixture.Controller(fixture.Manager);
        FileContentResult txt = Assert.IsType<FileContentResult>(await controller.ExportTxt(
            fixture.Project.Id, save.DocumentId!.Value, default));
        FileContentResult pdf = Assert.IsType<FileContentResult>(await controller.ExportPdf(
            fixture.Project.Id, save.DocumentId.Value, default));
        Assert.Equal(2, fixture.Gemini.CallCount);
        string text = Encoding.UTF8.GetString(txt.FileContents);
        Assert.Contains("FR-001", text);
        Assert.Contains("NFR-001", text);
        Assert.True(pdf.FileContents.Length > 1000);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(pdf.FileContents, 0, 4));
        Assert.Contains(fixture.Context.ActivityLogs, log => log.Action == "SrsTxtExported");
        Assert.Contains(fixture.Context.ActivityLogs, log => log.Action == "SrsPdfExported");
    }

    [Fact]
    public async Task ExportFilenameIsSanitizedAndForeignIdsAreRejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Project.Name = "../ Unsafe:Project";
        await fixture.Context.SaveChangesAsync();
        int id = await fixture.AddSavedDocumentAsync(fixture.Admin.Id, fixture.Project.Id);
        FileContentResult txt = Assert.IsType<FileContentResult>(await fixture.Controller(fixture.Admin)
            .ExportTxt(fixture.Project.Id, id, default));
        Assert.DoesNotContain("/", txt.FileDownloadName);
        Assert.DoesNotContain("\\", txt.FileDownloadName);
        Assert.EndsWith($"-srs-{id}.txt", txt.FileDownloadName);
        Assert.IsType<NotFoundObjectResult>(await fixture.Controller(fixture.Admin)
            .ExportTxt(fixture.ForeignProject.Id, id, default));
    }

    [Fact]
    public async Task ClientForgedQualityValuesDoNotBypassControllerGate()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Gemini.QualityResult = GeminiClientResult.Success(QualityJson(40, "Insufficient", false));
        SrsController controller = fixture.Controller(fixture.Manager);
        var model = new SrsIndexViewModel
        {
            ProjectId = fixture.Project.Id,
            QualityAnalysis = new Planora.Web.ViewModels.Requirements.AiInputQualityAnalysisViewModel
            {
                QualityScore = 100, QualityLevel = AiInputQualityLevel.Excellent, IsSufficient = true
            }
        };
        ViewResult result = Assert.IsType<ViewResult>(
            await controller.Generate(fixture.Project.Id, model, default));
        Assert.Equal(0, fixture.Gemini.SrsCallCount);
        Assert.Equal(40, Assert.IsType<SrsIndexViewModel>(result.Model).QualityAnalysis!.QualityScore);
    }

    [Fact]
    public void SrsViewsAndControllerPreserveCsrfAndXssSafety()
    {
        string root = RepositoryRoot();
        string views = string.Join('\n', Directory.GetFiles(
            Path.Combine(root, "src", "Planora.Web", "Views", "Srs"), "*.cshtml")
            .Select(File.ReadAllText));
        Assert.DoesNotContain("Html.Raw", views, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("AntiForgeryToken", views);
        foreach (string action in new[] { nameof(SrsController.Analyze), nameof(SrsController.Generate), nameof(SrsController.Save) })
        {
            MethodInfo method = typeof(SrsController).GetMethod(action)!;
            Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        }
        Assert.NotNull(typeof(SrsController).GetMethod(nameof(SrsController.Generate))!
            .GetCustomAttribute<EnableRateLimitingAttribute>());
        Assert.Null(typeof(GenerateSrsRequest).GetProperty("QualityScore"));
        Assert.Null(typeof(SaveSrsRequest).GetProperty("SavedByUserId"));
    }

    [Fact]
    public void FocusedMigrationCreatesOnlySrsDocumentsTable()
    {
        string root = RepositoryRoot();
        string migration = File.ReadAllText(Directory.GetFiles(
            Path.Combine(root, "src", "Planora.Infrastructure", "Persistence", "Migrations"),
            "*_AddSrsDocuments.cs").Single());
        Assert.Contains("name: \"SrsDocuments\"", migration);
        Assert.Equal(1, migration.Split("migrationBuilder.CreateTable", StringSplitOptions.None).Length - 1);
        Assert.Contains("ReferentialAction.Restrict", migration);
    }

    private static string QualityJson(int score, string level, bool sufficient) => JsonSerializer.Serialize(new
    {
        qualityScore = score, qualityLevel = level,
        validationMessage = sufficient ? "Sufficient." : "More information is required.",
        missingInformation = Array.Empty<string>(), issues = Array.Empty<string>(),
        suggestions = Array.Empty<string>(), improvementItems = Array.Empty<object>(), isSufficient = sufficient
    });

    private static string ValidSrsJson(bool duplicateFr = false)
    {
        var fr = new
        {
            identifier = "FR-001", name = "Authenticate users", priority = "High",
            description = "The system shall authenticate a registered user using verified credentials.",
            businessRationale = "Protect project access.", preconditions = "The user has a registered account.",
            exceptionScenario = "Invalid credentials are rejected."
        };
        return JsonSerializer.Serialize(new
        {
            documentTitle = "Identity Platform Software Requirements Specification",
            introduction = new { purpose = "Define approved requirements.", scope = "Identity workflows.", documentOverview = "This document describes the product requirements." },
            overallDescription = new { productPerspective = "A Planora project system.", productFunctions = "Authentication and controlled access.", userActorOverview = "Registered users and administrators.", operatingEnvironment = "Not specified." },
            actors = new[] { new { name = "Registered User", description = "Uses authenticated project features." } },
            functionalRequirements = duplicateFr ? new[] { fr, fr } : new[] { fr },
            nonFunctionalRequirements = new[] { new { identifier = "NFR-001", category = "Security", description = "The system shall protect authentication data.", priority = "Critical", rationale = "Reduce unauthorized access.", relatedFunctionalRequirements = new[] { "FR-001" } } },
            externalInterfaceRequirements = new[] { "Not specified." }, dataRequirements = new[] { "Store account identity data." },
            constraints = new[] { "Use approved project requirements." }, assumptionsAndDependencies = new[] { "Users have registered accounts." },
            acceptanceCriteria = new[] { "Valid credentials grant access and invalid credentials are rejected." },
            aiQualitySummary = "The source input was rated Good and sufficient for SRS generation."
        });
    }

    private static SrsContent CanonicalContent() => new()
    {
        DocumentTitle = "Saved SRS",
        Introduction = new SrsIntroduction { Purpose = "Purpose", Scope = "Scope", DocumentOverview = "Overview" },
        OverallDescription = new SrsOverallDescription { ProductPerspective = "Perspective", ProductFunctions = "Functions", UserActorOverview = "Actors", OperatingEnvironment = "Not specified." },
        Actors = [new SrsActor { Name = "User", Description = "Uses the system." }],
        FunctionalRequirements = [new SrsFunctionalRequirement { Identifier = "FR-001", Name = "Authenticate", Priority = "High", Description = "The system shall authenticate users.", BusinessRationale = "Protect access.", Preconditions = "Registered user.", ExceptionScenario = "Reject invalid credentials." }],
        NonFunctionalRequirements = [new SrsNonFunctionalRequirement { Identifier = "NFR-001", Category = "Security", Priority = "Critical", Description = "Protect credentials.", Rationale = "Reduce risk.", RelatedFunctionalRequirements = ["FR-001"] }],
        ExternalInterfaceRequirements = ["Not specified."], DataRequirements = ["Identity data."], Constraints = ["Approved requirements only."],
        AssumptionsAndDependencies = ["Registered users."], AcceptanceCriteria = ["Authentication succeeds with valid credentials."], AiQualitySummary = "Good input quality."
    };

    private static SrsContent CopyContent(
        SrsContent source, string? title = null, string? purpose = null,
        IReadOnlyList<SrsFunctionalRequirement>? functional = null) => new()
    {
        DocumentTitle = title ?? source.DocumentTitle,
        Introduction = new SrsIntroduction { Purpose = purpose ?? source.Introduction.Purpose, Scope = source.Introduction.Scope, DocumentOverview = source.Introduction.DocumentOverview },
        OverallDescription = source.OverallDescription, Actors = source.Actors,
        FunctionalRequirements = functional ?? source.FunctionalRequirements,
        NonFunctionalRequirements = source.NonFunctionalRequirements,
        ExternalInterfaceRequirements = source.ExternalInterfaceRequirements,
        DataRequirements = source.DataRequirements, Constraints = source.Constraints,
        AssumptionsAndDependencies = source.AssumptionsAndDependencies,
        AcceptanceCriteria = source.AcceptanceCriteria, AiQualitySummary = source.AiQualitySummary
    };

    private static string RepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Planora.sln"))) current = current.Parent;
        return current?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        public ApplicationDbContext Context { get; }
        public ISrsService Srs { get; }
        public IAiRequirementGenerationService Quality { get; }
        public FakeGeminiClient Gemini { get; }
        public Clock Clock { get; }
        public ApplicationUser Admin { get; }
        public ApplicationUser Manager { get; }
        public ApplicationUser Developer { get; }
        public ApplicationUser Qa { get; }
        public ApplicationUser ScrumMaster { get; }
        public ApplicationUser Outsider { get; }
        public Project Project { get; }
        public Project ForeignProject { get; }
        public Project ArchivedProject { get; }
        public Project ScrumProject { get; }
        public Requirement Fr { get; }
        public Requirement Nfr { get; }

        private Fixture(ServiceProvider provider, FakeGeminiClient gemini)
        {
            _provider = provider; _scope = provider.CreateScope(); Gemini = gemini;
            Context = _scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Srs = _scope.ServiceProvider.GetRequiredService<ISrsService>();
            Quality = _scope.ServiceProvider.GetRequiredService<IAiRequirementGenerationService>();
            Clock = (Clock)_scope.ServiceProvider.GetRequiredService<IClock>();
            var users = _scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var roles = _scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (string role in SystemRoles.All) Assert.True(roles.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult().Succeeded);
            Admin = AddUser(users, "admin", SystemRoles.Admin); Manager = AddUser(users, "manager", SystemRoles.ProjectManager);
            Developer = AddUser(users, "developer", SystemRoles.Developer); Qa = AddUser(users, "qa", SystemRoles.QaTester);
            ScrumMaster = AddUser(users, "scrum", SystemRoles.ScrumMaster); Outsider = AddUser(users, "outsider", SystemRoles.ProjectManager);
            Project = ProjectEntity("SRS Project", ProjectMethodology.VModel, ProjectStatus.Active);
            ForeignProject = ProjectEntity("Foreign", ProjectMethodology.VModel, ProjectStatus.Active);
            ArchivedProject = ProjectEntity("Archived", ProjectMethodology.VModel, ProjectStatus.Archived);
            ScrumProject = ProjectEntity("Scrum", ProjectMethodology.Scrum, ProjectStatus.Active);
            Context.AddRange(Project, ForeignProject, ArchivedProject, ScrumProject); Context.SaveChanges();
            Context.AddRange(
                Member(Project, Manager, ProjectMemberRole.ProjectManager), Member(Project, Developer, ProjectMemberRole.Developer),
                Member(Project, Qa, ProjectMemberRole.QaTester), Member(Project, ScrumMaster, ProjectMemberRole.ScrumMaster));
            Context.SaveChanges();
            Fr = RequirementEntity(Project.Id, "FR-001", RequirementType.Functional, PriorityLevel.High, Manager.Id);
            Nfr = RequirementEntity(Project.Id, "NFR-001", RequirementType.NonFunctional, PriorityLevel.Critical, Manager.Id);
            Nfr.NfrCategory = NfrCategory.Security;
            Context.AddRange(Fr, Nfr); Context.SaveChanges();
            Context.RequirementDependencies.Add(new RequirementDependency { RequirementId = Nfr.Id, DependsOnRequirementId = Fr.Id, CreatedByUserId = Manager.Id, CreatedAt = Clock.UtcNow });
            Context.SaveChanges();
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public static Task<Fixture> CreateAsync(bool configured = true)
        {
            var services = new ServiceCollection(); var root = new InMemoryDatabaseRoot();
            var gemini = new FakeGeminiClient { Configured = configured };
            services.AddLogging(); services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString(), root));
            services.AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();
            services.AddSingleton<IClock, Clock>(); services.AddSingleton<IGeminiClient>(gemini);
            services.AddSingleton<IAiInputQualityCache, InMemoryAiInputQualityCache>(); services.AddSingleton<ISrsDraftCache, InMemorySrsDraftCache>();
            services.AddScoped<IRequirementService, RequirementService>(); services.AddScoped<IAiRequirementGenerationService, AiRequirementGenerationService>();
            services.AddScoped<ISrsService, SrsService>();
            return Task.FromResult(new Fixture(services.BuildServiceProvider(), gemini));
        }

        public ApplicationUser User(string actor) => actor switch { "admin" => Admin, "manager" => Manager, "developer" => Developer, "qa" => Qa, "scrum" => ScrumMaster, _ => Outsider };
        public GenerateSrsRequest Generate(string actor, int? projectId = null, string? context = null) => new() { ProjectId = projectId ?? Project.Id, ActorUserId = actor, AdditionalContext = context };
        public AnalyzeAiRequirementInputRequest Analyze(string actor, string? context = null) => new() { ProjectId = Project.Id, ActorUserId = actor, Mode = AiRequirementGenerationMode.Both, SuggestionCount = 1, AdditionalContext = context };

        public async Task<int> AddSavedDocumentAsync(string actor, int projectId)
        {
            var document = new SrsDocument { ProjectId = projectId, Title = "Saved SRS", StructuredContentJson = JsonSerializer.Serialize(CanonicalContent(), new JsonSerializerOptions(JsonSerializerDefaults.Web)), QualityScore = 82, QualityLevel = "Good", GeneratedByUserId = actor, GeneratedAt = Clock.UtcNow, SavedByUserId = actor, SavedAt = Clock.UtcNow };
            Context.SrsDocuments.Add(document); await Context.SaveChangesAsync(); return document.Id;
        }

        public SrsController Controller(ApplicationUser user)
        {
            var controller = new SrsController(Srs); var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)], "Test");
            foreach (string role in Context.UserRoles.Where(item => item.UserId == user.Id).Join(Context.Roles, item => item.RoleId, role => role.Id, (_, role) => role.Name!).ToList()) identity.AddClaim(new Claim(ClaimTypes.Role, role));
            var http = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = new ClaimsPrincipal(identity) };
            controller.ControllerContext = new ControllerContext { HttpContext = http };
            controller.TempData = new TempDataDictionary(http, new NullTempDataProvider()); return controller;
        }

        private Project ProjectEntity(string name, ProjectMethodology methodology, ProjectStatus status) => new() { Name = name, Description = "Identity platform.", Objectives = "Secure access.", Scope = "Authentication.", Methodology = methodology, Status = status, StartDate = Clock.UtcNow, CreatedByUserId = Admin.Id, CreatedAt = Clock.UtcNow };
        private static ProjectMember Member(Project project, ApplicationUser user, ProjectMemberRole role) => new() { Project = project, UserId = user.Id, Role = role };
        private Requirement RequirementEntity(int projectId, string identifier, RequirementType type, PriorityLevel priority, string actor) => new() { ProjectId = projectId, Identifier = identifier, Title = type == RequirementType.Functional ? "Authenticate users" : "Protect credentials", Description = type == RequirementType.Functional ? "The system shall authenticate registered users." : "The system shall protect authentication data.", Type = type, Priority = priority, Rationale = "Project requirement rationale.", Preconditions = type == RequirementType.Functional ? "Registered account." : null, ExceptionScenario = type == RequirementType.Functional ? "Reject invalid credentials." : null, Status = RequirementStatus.Approved, CreatedByUserId = actor, CreatedAt = Clock.UtcNow };
        private static ApplicationUser AddUser(UserManager<ApplicationUser> manager, string name, string role) { var user = new ApplicationUser { UserName = $"{name}@srs.test", Email = $"{name}@srs.test", FullName = name, EmailConfirmed = true }; Assert.True(manager.CreateAsync(user).GetAwaiter().GetResult().Succeeded); Assert.True(manager.AddToRoleAsync(user, role).GetAwaiter().GetResult().Succeeded); return user; }
        public async ValueTask DisposeAsync() { _scope.Dispose(); await _provider.DisposeAsync(); }
    }

    private sealed class FakeGeminiClient : IGeminiClient
    {
        public bool Configured { get; set; } = true;
        public bool IsConfigured => Configured;
        public GeminiClientResult QualityResult { get; set; } = GeminiClientResult.Success(QualityJson(82, "Good", true));
        public GeminiClientResult SrsResult { get; set; } = GeminiClientResult.Success(ValidSrsJson());
        public int CallCount { get; private set; }
        public int QualityCallCount { get; private set; }
        public int SrsCallCount { get; private set; }
        public Task<GeminiClientResult> GenerateJsonAsync(GeminiClientRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); CallCount++;
            if (request.Prompt.Contains("ANALYSIS REQUEST:", StringComparison.Ordinal)) { QualityCallCount++; return Task.FromResult(QualityResult); }
            SrsCallCount++; return Task.FromResult(SrsResult);
        }
    }

    public sealed class Clock : IClock { public DateTime UtcNow { get; set; } = new(2026, 9, 9, 20, 0, 0, DateTimeKind.Utc); }
    private sealed class NullTempDataProvider : ITempDataProvider { public IDictionary<string, object> LoadTempData(Microsoft.AspNetCore.Http.HttpContext context) => new Dictionary<string, object>(); public void SaveTempData(Microsoft.AspNetCore.Http.HttpContext context, IDictionary<string, object> values) { } }
}
