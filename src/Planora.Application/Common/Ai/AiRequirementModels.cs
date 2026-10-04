using Planora.Domain.Enums;

namespace Planora.Application.Common.Ai;

public enum AiRequirementGenerationMode
{
    Functional = 1,
    NonFunctional = 2,
    Both = 3
}

public enum AiRequirementFailure
{
    None = 0,
    Validation,
    NotFound,
    Forbidden,
    ReadOnly,
    ConfigurationUnavailable,
    TemporarilyUnavailable,
    InvalidResponse,
    Conflict
}

public enum AiInputQualityLevel
{
    Insufficient = 1,
    NeedsImprovement = 2,
    Good = 3,
    Excellent = 4
}

public enum AiImprovementInputType
{
    SingleChoice = 1,
    FreeText = 2
}

public static class AiInputQualityPolicy
{
    public const int PassingScore = 70;

    public static AiInputQualityLevel GetLevel(int score) => score switch
    {
        >= 0 and <= 49 => AiInputQualityLevel.Insufficient,
        >= 50 and <= 69 => AiInputQualityLevel.NeedsImprovement,
        >= 70 and <= 84 => AiInputQualityLevel.Good,
        >= 85 and <= 100 => AiInputQualityLevel.Excellent,
        _ => throw new ArgumentOutOfRangeException(nameof(score))
    };

    public static bool CanGenerate(int score, bool isSufficient) =>
        score >= PassingScore && score <= 100 && isSufficient;
}

public enum GeminiClientFailure
{
    None = 0,
    ConfigurationUnavailable,
    Authentication,
    RateLimited,
    Timeout,
    Connection,
    InvalidResponse,
    Provider
}

public sealed class GeminiClientRequest
{
    public string Prompt { get; init; } = string.Empty;
}

public sealed class GeminiClientResult
{
    public bool Succeeded { get; init; }
    public string Json { get; init; } = string.Empty;
    public GeminiClientFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;

    public static GeminiClientResult Success(string json) => new()
        { Succeeded = true, Json = json };

    public static GeminiClientResult Failed(GeminiClientFailure failure, string message) => new()
        { Failure = failure, Message = message };
}

public sealed class GenerateAiRequirementsRequest
{
    public int ProjectId { get; init; }
    public AiRequirementGenerationMode Mode { get; init; }
    public int SuggestionCount { get; init; }
    public string? AdditionalContext { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class AnalyzeAiRequirementInputRequest
{
    public int ProjectId { get; init; }
    public AiRequirementGenerationMode Mode { get; init; }
    public int SuggestionCount { get; init; }
    public string? AdditionalContext { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class AiInputImprovementItem
{
    public string Id { get; init; } = string.Empty;
    public string Topic { get; init; } = string.Empty;
    public string Question { get; init; } = string.Empty;
    public AiImprovementInputType InputType { get; init; }
    public IReadOnlyList<string> Options { get; init; } = [];
    public string? SuggestedText { get; init; }
}

public sealed class AiInputQualityAnalysisResult
{
    public bool Succeeded { get; init; }
    public AiRequirementFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;
    public int QualityScore { get; init; }
    public AiInputQualityLevel QualityLevel { get; init; }
    public string ValidationMessage { get; init; } = string.Empty;
    public IReadOnlyList<string> MissingInformation { get; init; } = [];
    public IReadOnlyList<string> Issues { get; init; } = [];
    public IReadOnlyList<string> Suggestions { get; init; } = [];
    public IReadOnlyList<AiInputImprovementItem> ImprovementItems { get; init; } = [];
    public bool IsSufficient { get; init; }

    public bool CanGenerate => Succeeded &&
        AiInputQualityPolicy.CanGenerate(QualityScore, IsSufficient);
}

public sealed class AiInputQualityCacheRecord
{
    public int QualityScore { get; init; }
    public AiInputQualityLevel QualityLevel { get; init; }
    public bool IsSufficient { get; init; }
    public DateTime AnalyzedAt { get; init; }

    public bool CanGenerate => AiInputQualityPolicy.CanGenerate(QualityScore, IsSufficient);
}

public sealed class AiFunctionalRequirementDraft
{
    public string RequirementName { get; init; } = string.Empty;
    public PriorityLevel Priority { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? BusinessRationale { get; init; }
    public string? Preconditions { get; init; }
    public string? ExceptionScenario { get; init; }
}

public sealed class AiNonFunctionalRequirementDraft
{
    public NfrCategory Category { get; init; }
    public string RequirementDescription { get; init; } = string.Empty;
    public PriorityLevel Priority { get; init; }
    public string? Rationale { get; init; }
    public IReadOnlyList<string> RelatedFunctionalRequirements { get; init; } = [];
}

public sealed class AiRequirementGenerationResult
{
    public bool Succeeded { get; init; }
    public AiRequirementFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<AiFunctionalRequirementDraft> FunctionalRequirements { get; init; } = [];
    public IReadOnlyList<AiNonFunctionalRequirementDraft> NonFunctionalRequirements { get; init; } = [];
    public AiInputQualityAnalysisResult? QualityAnalysis { get; init; }
}

public sealed class SaveAiRequirementsRequest
{
    public int ProjectId { get; init; }
    public IReadOnlyList<AiFunctionalRequirementDraft> FunctionalRequirements { get; init; } = [];
    public IReadOnlyList<AiNonFunctionalRequirementDraft> NonFunctionalRequirements { get; init; } = [];
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class AiRequirementSaveResult
{
    public bool Succeeded { get; init; }
    public AiRequirementFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<int> RequirementIds { get; init; } = [];
}
