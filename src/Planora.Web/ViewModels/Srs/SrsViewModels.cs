using System.ComponentModel.DataAnnotations;
using Planora.Application.Common.Ai;
using Planora.Application.Common.Srs;
using Planora.Web.ViewModels.Requirements;

namespace Planora.Web.ViewModels.Srs;

public sealed class SrsIndexViewModel
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public bool CanManage { get; set; }
    public bool IsReadOnly { get; set; }
    public bool IsConfigured { get; set; }
    [StringLength(3000)]
    public string? AdditionalContext { get; set; }
    public AiInputQualityAnalysisViewModel? QualityAnalysis { get; set; }
    public IReadOnlyList<SrsDocumentSummary> Documents { get; set; } = [];
}

public sealed class SrsReviewViewModel
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string DraftToken { get; set; } = string.Empty;
    public int QualityScore { get; set; }
    public AiInputQualityLevel QualityLevel { get; set; }
    public DateTime GeneratedAt { get; set; }
    public SrsContentEditViewModel Content { get; set; } = new();
}

public sealed class SrsContentEditViewModel
{
    [Required, StringLength(200)] public string DocumentTitle { get; set; } = string.Empty;
    [Required, StringLength(8000)] public string Purpose { get; set; } = string.Empty;
    [Required, StringLength(8000)] public string Scope { get; set; } = string.Empty;
    [Required, StringLength(8000)] public string DocumentOverview { get; set; } = string.Empty;
    [Required, StringLength(8000)] public string ProductPerspective { get; set; } = string.Empty;
    [Required, StringLength(8000)] public string ProductFunctions { get; set; } = string.Empty;
    [Required, StringLength(8000)] public string UserActorOverview { get; set; } = string.Empty;
    [Required, StringLength(8000)] public string OperatingEnvironment { get; set; } = string.Empty;
    public List<SrsActorEditViewModel> Actors { get; set; } = [];
    public List<SrsFunctionalRequirementEditViewModel> FunctionalRequirements { get; set; } = [];
    public List<SrsNonFunctionalRequirementEditViewModel> NonFunctionalRequirements { get; set; } = [];
    public string ExternalInterfaceRequirementsText { get; set; } = string.Empty;
    public string DataRequirementsText { get; set; } = string.Empty;
    public string ConstraintsText { get; set; } = string.Empty;
    public string AssumptionsAndDependenciesText { get; set; } = string.Empty;
    public string AcceptanceCriteriaText { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string AiQualitySummary { get; set; } = string.Empty;
}

public sealed class SrsActorEditViewModel
{
    [Required, StringLength(100)] public string Name { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Description { get; set; } = string.Empty;
}

public sealed class SrsFunctionalRequirementEditViewModel
{
    public string Identifier { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Name { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    [Required, StringLength(5000)] public string Description { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string BusinessRationale { get; set; } = string.Empty;
    [Required, StringLength(3000)] public string Preconditions { get; set; } = string.Empty;
    [Required, StringLength(3000)] public string ExceptionScenario { get; set; } = string.Empty;
}

public sealed class SrsNonFunctionalRequirementEditViewModel
{
    public string Identifier { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    [Required, StringLength(5000)] public string Description { get; set; } = string.Empty;
    [Required, StringLength(4000)] public string Rationale { get; set; } = string.Empty;
    public List<string> RelatedFunctionalRequirements { get; set; } = [];
}
