using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Planora.Application.Abstractions.Srs;
using Planora.Application.Common.Ai;
using Planora.Application.Common.Security;
using Planora.Application.Common.Srs;
using Planora.Web.Documents;
using Planora.Web.ViewModels.Requirements;
using Planora.Web.ViewModels.Srs;

namespace Planora.Web.Controllers;

[Authorize]
public sealed class SrsController : Controller
{
    private readonly ISrsService _service;

    public SrsController(ISrsService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Index(int projectId, CancellationToken cancellationToken)
    {
        SrsListResult result = await _service.GetDocumentsAsync(projectId, UserId(), cancellationToken);
        if (!result.Succeeded) return Failure(result.Failure, result.Message);
        return View(ToIndexModel(projectId, result));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.AiRequirementGeneration)]
    public async Task<IActionResult> Analyze(
        int projectId, SrsIndexViewModel model, CancellationToken cancellationToken)
    {
        if (model.ProjectId != projectId) return BadRequest("The project reference is invalid.");
        SrsListResult list = await _service.GetDocumentsAsync(projectId, UserId(), cancellationToken);
        if (!list.Succeeded) return Failure(list.Failure, list.Message);
        if (!ModelState.IsValid) return View("Index", ToIndexModel(projectId, list, model.AdditionalContext));
        SrsGenerationResult result = await _service.AnalyzeInputAsync(new GenerateSrsRequest
        {
            ProjectId = projectId,
            ActorUserId = UserId(),
            AdditionalContext = model.AdditionalContext
        }, cancellationToken);
        if (!result.Succeeded)
        {
            if (IsDisplayable(result.Failure))
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View("Index", ToIndexModel(projectId, list, model.AdditionalContext,
                    result.QualityAnalysis));
            }
            return Failure(result.Failure, result.Message);
        }
        return View("Index", ToIndexModel(projectId, list, model.AdditionalContext,
            result.QualityAnalysis));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [EnableRateLimiting(RateLimitPolicies.AiRequirementGeneration)]
    public async Task<IActionResult> Generate(
        int projectId, SrsIndexViewModel model, CancellationToken cancellationToken)
    {
        if (model.ProjectId != projectId) return BadRequest("The project reference is invalid.");
        if (!ModelState.IsValid)
        {
            SrsListResult invalidList = await _service.GetDocumentsAsync(projectId, UserId(), cancellationToken);
            if (!invalidList.Succeeded) return Failure(invalidList.Failure, invalidList.Message);
            return View("Index", ToIndexModel(projectId, invalidList, model.AdditionalContext));
        }
        SrsGenerationResult result = await _service.GenerateAsync(new GenerateSrsRequest
        {
            ProjectId = projectId,
            ActorUserId = UserId(),
            AdditionalContext = model.AdditionalContext
        }, cancellationToken);
        if (!result.Succeeded)
        {
            if (IsDisplayable(result.Failure))
            {
                SrsListResult list = await _service.GetDocumentsAsync(projectId, UserId(), cancellationToken);
                if (!list.Succeeded) return Failure(list.Failure, list.Message);
                ModelState.AddModelError(string.Empty, result.Message);
                return View("Index", ToIndexModel(projectId, list, model.AdditionalContext,
                    result.QualityAnalysis));
            }
            return Failure(result.Failure, result.Message);
        }
        return RedirectToAction(nameof(Review), new { projectId, token = result.Draft!.Token });
    }

    [HttpGet]
    public async Task<IActionResult> Review(
        int projectId, string token, CancellationToken cancellationToken)
    {
        SrsDraftResult result = await _service.GetDraftAsync(projectId, token, UserId(), cancellationToken);
        if (!result.Succeeded) return Failure(result.Failure, result.Message);
        return View(ToReviewModel(result.Draft!));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        int projectId, SrsReviewViewModel model, CancellationToken cancellationToken)
    {
        if (model.ProjectId != projectId) return BadRequest("The project reference is invalid.");
        if (!ModelState.IsValid) return View("Review", model);
        SrsSaveResult result = await _service.SaveAsync(new SaveSrsRequest
        {
            ProjectId = projectId,
            DraftToken = model.DraftToken,
            ActorUserId = UserId(),
            Content = ToContent(model.Content)
        }, cancellationToken);
        if (!result.Succeeded)
        {
            if (IsDisplayable(result.Failure))
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return View("Review", model);
            }
            return Failure(result.Failure, result.Message);
        }
        TempData["SuccessMessage"] = result.Message;
        return RedirectToAction(nameof(Details), new { projectId, id = result.DocumentId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(
        int projectId, int id, CancellationToken cancellationToken)
    {
        SrsDocumentResult result = await _service.GetDocumentAsync(projectId, id, UserId(), cancellationToken);
        return result.Succeeded ? View(result.Document) : Failure(result.Failure, result.Message);
    }

    [HttpGet]
    public async Task<IActionResult> ExportTxt(
        int projectId, int id, CancellationToken cancellationToken)
    {
        SrsDocumentResult result = await _service.GetDocumentAsync(projectId, id, UserId(), cancellationToken);
        if (!result.Succeeded) return Failure(result.Failure, result.Message);
        string content = SrsTextDocument.Render(result.Document!);
        SrsOperationResult audit = await _service.RecordExportAsync(
            projectId, id, UserId(), "TXT", cancellationToken);
        if (!audit.Succeeded) return Failure(audit.Failure, audit.Message);
        return File(new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetBytes(content),
            "text/plain; charset=utf-8", FileName(result.Document!.ProjectName, id, "txt"));
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(
        int projectId, int id, CancellationToken cancellationToken)
    {
        SrsDocumentResult result = await _service.GetDocumentAsync(projectId, id, UserId(), cancellationToken);
        if (!result.Succeeded) return Failure(result.Failure, result.Message);
        byte[] content = new SrsPdfDocument(result.Document!).Generate();
        SrsOperationResult audit = await _service.RecordExportAsync(
            projectId, id, UserId(), "PDF", cancellationToken);
        if (!audit.Succeeded) return Failure(audit.Failure, audit.Message);
        return File(content, "application/pdf", FileName(result.Document!.ProjectName, id, "pdf"));
    }

    private string UserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private SrsIndexViewModel ToIndexModel(
        int projectId, SrsListResult result, string? context = null,
        AiInputQualityAnalysisResult? quality = null) => new()
    {
        ProjectId = projectId,
        ProjectName = result.ProjectName,
        CanManage = result.CanManage,
        IsReadOnly = result.IsReadOnly,
        IsConfigured = _service.IsConfigured,
        AdditionalContext = context,
        Documents = result.Documents,
        QualityAnalysis = quality is null ? null : new AiInputQualityAnalysisViewModel
        {
            QualityScore = quality.QualityScore,
            QualityLevel = quality.QualityLevel,
            ValidationMessage = quality.ValidationMessage,
            MissingInformation = quality.MissingInformation.ToList(),
            Issues = quality.Issues.ToList(),
            Suggestions = quality.Suggestions.ToList(),
            ImprovementItems = quality.ImprovementItems.Select(item => new AiInputImprovementViewModel
            {
                Id = item.Id, Topic = item.Topic, Question = item.Question,
                InputType = item.InputType, Options = item.Options.ToList(),
                SuggestedText = item.SuggestedText
            }).ToList(),
            IsSufficient = quality.IsSufficient
        }
    };

    private static SrsReviewViewModel ToReviewModel(SrsDraft draft) => new()
    {
        ProjectId = draft.ProjectId,
        ProjectName = draft.ProjectName,
        DraftToken = draft.Token,
        QualityScore = draft.QualityScore,
        QualityLevel = draft.QualityLevel,
        GeneratedAt = draft.GeneratedAt,
        Content = new SrsContentEditViewModel
        {
            DocumentTitle = draft.Content.DocumentTitle,
            Purpose = draft.Content.Introduction.Purpose,
            Scope = draft.Content.Introduction.Scope,
            DocumentOverview = draft.Content.Introduction.DocumentOverview,
            ProductPerspective = draft.Content.OverallDescription.ProductPerspective,
            ProductFunctions = draft.Content.OverallDescription.ProductFunctions,
            UserActorOverview = draft.Content.OverallDescription.UserActorOverview,
            OperatingEnvironment = draft.Content.OverallDescription.OperatingEnvironment,
            Actors = draft.Content.Actors.Select(actor => new SrsActorEditViewModel
                { Name = actor.Name, Description = actor.Description }).ToList(),
            FunctionalRequirements = draft.Content.FunctionalRequirements.Select(requirement =>
                new SrsFunctionalRequirementEditViewModel
                {
                    Identifier = requirement.Identifier, Name = requirement.Name,
                    Priority = requirement.Priority, Description = requirement.Description,
                    BusinessRationale = requirement.BusinessRationale,
                    Preconditions = requirement.Preconditions,
                    ExceptionScenario = requirement.ExceptionScenario
                }).ToList(),
            NonFunctionalRequirements = draft.Content.NonFunctionalRequirements.Select(requirement =>
                new SrsNonFunctionalRequirementEditViewModel
                {
                    Identifier = requirement.Identifier, Category = requirement.Category,
                    Priority = requirement.Priority, Description = requirement.Description,
                    Rationale = requirement.Rationale,
                    RelatedFunctionalRequirements = requirement.RelatedFunctionalRequirements.ToList()
                }).ToList(),
            ExternalInterfaceRequirementsText = JoinLines(draft.Content.ExternalInterfaceRequirements),
            DataRequirementsText = JoinLines(draft.Content.DataRequirements),
            ConstraintsText = JoinLines(draft.Content.Constraints),
            AssumptionsAndDependenciesText = JoinLines(draft.Content.AssumptionsAndDependencies),
            AcceptanceCriteriaText = JoinLines(draft.Content.AcceptanceCriteria),
            AiQualitySummary = draft.Content.AiQualitySummary
        }
    };

    private static SrsContent ToContent(SrsContentEditViewModel content) => new()
    {
        DocumentTitle = content.DocumentTitle,
        Introduction = new SrsIntroduction
        {
            Purpose = content.Purpose, Scope = content.Scope, DocumentOverview = content.DocumentOverview
        },
        OverallDescription = new SrsOverallDescription
        {
            ProductPerspective = content.ProductPerspective, ProductFunctions = content.ProductFunctions,
            UserActorOverview = content.UserActorOverview, OperatingEnvironment = content.OperatingEnvironment
        },
        Actors = content.Actors.Select(actor => new SrsActor
            { Name = actor.Name, Description = actor.Description }).ToList(),
        FunctionalRequirements = content.FunctionalRequirements.Select(requirement => new SrsFunctionalRequirement
        {
            Identifier = requirement.Identifier, Name = requirement.Name, Priority = requirement.Priority,
            Description = requirement.Description, BusinessRationale = requirement.BusinessRationale,
            Preconditions = requirement.Preconditions, ExceptionScenario = requirement.ExceptionScenario
        }).ToList(),
        NonFunctionalRequirements = content.NonFunctionalRequirements.Select(requirement =>
            new SrsNonFunctionalRequirement
            {
                Identifier = requirement.Identifier, Category = requirement.Category,
                Priority = requirement.Priority, Description = requirement.Description,
                Rationale = requirement.Rationale,
                RelatedFunctionalRequirements = requirement.RelatedFunctionalRequirements
            }).ToList(),
        ExternalInterfaceRequirements = SplitLines(content.ExternalInterfaceRequirementsText),
        DataRequirements = SplitLines(content.DataRequirementsText),
        Constraints = SplitLines(content.ConstraintsText),
        AssumptionsAndDependencies = SplitLines(content.AssumptionsAndDependenciesText),
        AcceptanceCriteria = SplitLines(content.AcceptanceCriteriaText),
        AiQualitySummary = content.AiQualitySummary
    };

    private static IReadOnlyList<string> SplitLines(string? value) => (value ?? string.Empty)
        .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    private static string JoinLines(IReadOnlyList<string> values) => string.Join(Environment.NewLine, values);
    private static string FileName(string projectName, int id, string extension)
    {
        string safe = new(projectName.ToLowerInvariant().Select(character =>
            char.IsAsciiLetterOrDigit(character) ? character : '-').ToArray());
        safe = string.Join('-', safe.Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (safe.Length == 0) safe = "planora-project";
        if (safe.Length > 80) safe = safe[..80].TrimEnd('-');
        return $"{safe}-srs-{id}.{extension}";
    }
    private static bool IsDisplayable(SrsOperationFailure failure) => failure is
        SrsOperationFailure.Validation or SrsOperationFailure.ConfigurationUnavailable or
        SrsOperationFailure.TemporarilyUnavailable or SrsOperationFailure.InvalidResponse or
        SrsOperationFailure.Expired;
    private IActionResult Failure(SrsOperationFailure failure, string message) => failure switch
    {
        SrsOperationFailure.NotFound or SrsOperationFailure.Expired => NotFound(message),
        SrsOperationFailure.Forbidden => StatusCode(StatusCodes.Status403Forbidden, message),
        SrsOperationFailure.ReadOnly => StatusCode(StatusCodes.Status409Conflict, message),
        _ => BadRequest(message)
    };
}
