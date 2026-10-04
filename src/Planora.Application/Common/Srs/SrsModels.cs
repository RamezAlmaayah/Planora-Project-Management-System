using Planora.Application.Common.Ai;

namespace Planora.Application.Common.Srs;

public enum SrsOperationFailure
{
    None = 0,
    Validation,
    NotFound,
    Forbidden,
    ReadOnly,
    ConfigurationUnavailable,
    TemporarilyUnavailable,
    InvalidResponse,
    Expired
}

public sealed class SrsIntroduction
{
    public string Purpose { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string DocumentOverview { get; init; } = string.Empty;
}

public sealed class SrsOverallDescription
{
    public string ProductPerspective { get; init; } = string.Empty;
    public string ProductFunctions { get; init; } = string.Empty;
    public string UserActorOverview { get; init; } = string.Empty;
    public string OperatingEnvironment { get; init; } = string.Empty;
}

public sealed class SrsActor
{
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class SrsFunctionalRequirement
{
    public string Identifier { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string BusinessRationale { get; init; } = string.Empty;
    public string Preconditions { get; init; } = string.Empty;
    public string ExceptionScenario { get; init; } = string.Empty;
}

public sealed class SrsNonFunctionalRequirement
{
    public string Identifier { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string Rationale { get; init; } = string.Empty;
    public IReadOnlyList<string> RelatedFunctionalRequirements { get; init; } = [];
}

public sealed class SrsContent
{
    public string DocumentTitle { get; init; } = string.Empty;
    public SrsIntroduction Introduction { get; init; } = new();
    public SrsOverallDescription OverallDescription { get; init; } = new();
    public IReadOnlyList<SrsActor> Actors { get; init; } = [];
    public IReadOnlyList<SrsFunctionalRequirement> FunctionalRequirements { get; init; } = [];
    public IReadOnlyList<SrsNonFunctionalRequirement> NonFunctionalRequirements { get; init; } = [];
    public IReadOnlyList<string> ExternalInterfaceRequirements { get; init; } = [];
    public IReadOnlyList<string> DataRequirements { get; init; } = [];
    public IReadOnlyList<string> Constraints { get; init; } = [];
    public IReadOnlyList<string> AssumptionsAndDependencies { get; init; } = [];
    public IReadOnlyList<string> AcceptanceCriteria { get; init; } = [];
    public string AiQualitySummary { get; init; } = string.Empty;
}

public sealed class GenerateSrsRequest
{
    public int ProjectId { get; init; }
    public string? AdditionalContext { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class SrsDraft
{
    public string Token { get; init; } = string.Empty;
    public int ProjectId { get; init; }
    public string ProjectName { get; init; } = string.Empty;
    public string OwnerUserId { get; init; } = string.Empty;
    public SrsContent Content { get; init; } = new();
    public int QualityScore { get; init; }
    public AiInputQualityLevel QualityLevel { get; init; }
    public string GeneratedByUserId { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
}

public sealed class SrsGenerationResult
{
    public bool Succeeded { get; init; }
    public SrsOperationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;
    public SrsDraft? Draft { get; init; }
    public AiInputQualityAnalysisResult? QualityAnalysis { get; init; }
}

public sealed class SaveSrsRequest
{
    public int ProjectId { get; init; }
    public string DraftToken { get; init; } = string.Empty;
    public string ActorUserId { get; init; } = string.Empty;
    public SrsContent Content { get; init; } = new();
}

public sealed class SrsSaveResult
{
    public bool Succeeded { get; init; }
    public SrsOperationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;
    public int? DocumentId { get; init; }
}

public class SrsDocumentSummary
{
    public int Id { get; init; }
    public int ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public int QualityScore { get; init; }
    public string QualityLevel { get; init; } = string.Empty;
    public string GeneratedByName { get; init; } = string.Empty;
    public DateTime GeneratedAt { get; init; }
    public string SavedByName { get; init; } = string.Empty;
    public DateTime SavedAt { get; init; }
}

public sealed class SrsDocumentDetails : SrsDocumentSummary
{
    public string ProjectName { get; init; } = string.Empty;
    public SrsContent Content { get; init; } = new();
}

public sealed class SrsListResult
{
    public bool Succeeded { get; init; }
    public SrsOperationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;
    public string ProjectName { get; init; } = string.Empty;
    public bool CanManage { get; init; }
    public bool IsReadOnly { get; init; }
    public IReadOnlyList<SrsDocumentSummary> Documents { get; init; } = [];
}

public sealed class SrsDraftResult
{
    public bool Succeeded { get; init; }
    public SrsOperationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;
    public SrsDraft? Draft { get; init; }
}

public sealed class SrsDocumentResult
{
    public bool Succeeded { get; init; }
    public SrsOperationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;
    public SrsDocumentDetails? Document { get; init; }
}

public sealed class SrsOperationResult
{
    public bool Succeeded { get; init; }
    public SrsOperationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;
}
