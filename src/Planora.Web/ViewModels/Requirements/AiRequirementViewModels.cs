using System.ComponentModel.DataAnnotations;
using Planora.Application.Common.Requirements;
using Planora.Application.Common.Ai;
using Planora.Domain.Enums;

namespace Planora.Web.ViewModels.Requirements;

public sealed class AiRequirementGeneratorViewModel
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectDescription { get; set; } = string.Empty;
    public string ProjectObjectives { get; set; } = string.Empty;
    public string ProjectScope { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
    public bool IsReadOnly { get; set; }

    [EnumDataType(typeof(AiRequirementGenerationMode))]
    public AiRequirementGenerationMode Mode { get; set; } = AiRequirementGenerationMode.Both;

    [Range(1, 10)]
    public int SuggestionCount { get; set; } = 3;

    [StringLength(3000)]
    public string? AdditionalContext { get; set; }

    public AiInputQualityAnalysisViewModel? QualityAnalysis { get; set; }
}

public sealed class AiInputQualityAnalysisViewModel
{
    public int QualityScore { get; set; }
    public AiInputQualityLevel QualityLevel { get; set; }
    public string ValidationMessage { get; set; } = string.Empty;
    public List<string> MissingInformation { get; set; } = [];
    public List<string> Issues { get; set; } = [];
    public List<string> Suggestions { get; set; } = [];
    public List<AiInputImprovementViewModel> ImprovementItems { get; set; } = [];
    public bool IsSufficient { get; set; }
    public bool CanGenerate => AiInputQualityPolicy.CanGenerate(QualityScore, IsSufficient);
}

public sealed class AiInputImprovementViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public AiImprovementInputType InputType { get; set; }
    public List<string> Options { get; set; } = [];
    public string? SuggestedText { get; set; }
}

public sealed class AiRequirementReviewViewModel
{
    public int ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public List<string> Warnings { get; set; } = [];
    public List<AiFunctionalRequirementDraftViewModel> FunctionalRequirements { get; set; } = [];
    public List<AiNonFunctionalRequirementDraftViewModel> NonFunctionalRequirements { get; set; } = [];
    public IReadOnlyList<RequirementOption> FunctionalRequirementOptions { get; set; } = [];
}

public sealed class AiFunctionalRequirementDraftViewModel
{
    public bool Include { get; set; } = true;
    public string RequirementName { get; set; } = string.Empty;
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;
    public string Description { get; set; } = string.Empty;
    public string? BusinessRationale { get; set; }
    public string? Preconditions { get; set; }
    public string? ExceptionScenario { get; set; }
}

public sealed class AiNonFunctionalRequirementDraftViewModel
{
    public bool Include { get; set; } = true;
    public NfrCategory Category { get; set; }
    public string RequirementDescription { get; set; } = string.Empty;
    public PriorityLevel Priority { get; set; } = PriorityLevel.Medium;
    public string? Rationale { get; set; }
    public List<string> RelatedFunctionalRequirements { get; set; } = [];
}
