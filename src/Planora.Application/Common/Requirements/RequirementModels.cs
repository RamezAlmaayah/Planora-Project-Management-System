using Planora.Domain.Enums;

namespace Planora.Application.Common.Requirements;

public sealed class RequirementFilter
{
    public RequirementType? Type { get; init; }
    public RequirementStatus? Status { get; init; }
    public PriorityLevel? Priority { get; init; }
}

public class RequirementSummary
{
    public int Id { get; init; }
    public int ProjectId { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public RequirementType Type { get; init; }
    public NfrCategory? NfrCategory { get; init; }
    public PriorityLevel Priority { get; init; }
    public RequirementStatus Status { get; init; }
    public string CreatorName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class RequirementDetails : RequirementSummary
{
    public string ProjectName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? Rationale { get; init; }
    public string? Preconditions { get; init; }
    public string? ExceptionScenario { get; init; }
    public string? UpdatedByName { get; init; }
    public IReadOnlyList<RequirementDependencySummary> Dependencies { get; set; } = [];
    public IReadOnlyList<RequirementDependencySummary> Dependents { get; set; } = [];
    public IReadOnlyList<RequirementDependencySummary> RelatedFunctionalRequirements { get; set; } = [];
    public IReadOnlyList<RequirementTraceSummary> Traces { get; set; } = [];
    public IReadOnlyList<RequirementDesignArtifactSummary> DesignArtifacts { get; set; } = [];
    public IReadOnlyList<RequirementImplementationArtifactSummary> ImplementationArtifacts { get; set; } = [];
}

public sealed class RequirementDependencySummary
{
    public int LinkId { get; init; }
    public int RequirementId { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public RequirementType Type { get; init; }
}

public sealed class RequirementTraceSummary
{
    public int Id { get; init; }
    public RequirementTraceStage Stage { get; init; }
    public string ReferenceCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class RequirementDesignArtifactSummary
{
    public int Id { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DesignArtifactType Type { get; init; }
    public IReadOnlyList<RequirementImplementationArtifactSummary> ImplementationArtifacts { get; set; } = [];
}

public sealed class RequirementImplementationArtifactSummary
{
    public int Id { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public ImplementationArtifactType Type { get; init; }
    public IReadOnlyList<RequirementTestCaseSummary> TestCases { get; set; } = [];
}

public sealed class RequirementTestCaseSummary
{
    public int Id { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public VModelTestLevel TestLevel { get; init; }
    public VModelTestResult? LatestResult { get; init; }
    public DateTime? LastExecutedAt { get; init; }
}

public sealed class RequirementPhaseValidationSummary
{
    public VModelPhaseType PhaseType { get; init; }
    public VModelValidationResult Result { get; init; }
    public DateTime ValidatedAt { get; init; }
}

public sealed class RequirementOption
{
    public int Id { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}

public sealed class RequirementTraceabilityItem
{
    public int RequirementId { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public RequirementType Type { get; init; }
    public RequirementStatus Status { get; init; }
    public IReadOnlyList<RequirementDependencySummary> Dependencies { get; init; } = [];
    public IReadOnlyList<RequirementTraceSummary> Traces { get; init; } = [];
    public IReadOnlyList<RequirementDesignArtifactSummary> DesignArtifacts { get; init; } = [];
    public RequirementPhaseValidationSummary? LatestPhaseValidation { get; init; }
    public RequirementCoverageState CoverageState { get; init; }
}

public enum RequirementOperationFailure
{
    None = 0,
    Validation,
    NotFound,
    Forbidden,
    ReadOnly,
    Conflict
}

public sealed class RequirementOperationResult
{
    public bool Succeeded { get; init; }
    public int? RequirementId { get; init; }
    public int? LinkId { get; init; }
    public string? Identifier { get; init; }
    public RequirementStatus? Status { get; init; }
    public RequirementOperationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;

    public static RequirementOperationResult Success(
        int requirementId,
        string identifier,
        RequirementStatus status,
        string message,
        int? linkId = null) => new()
        {
            Succeeded = true,
            RequirementId = requirementId,
            LinkId = linkId,
            Identifier = identifier,
            Status = status,
            Message = message
        };

    public static RequirementOperationResult Failed(
        RequirementOperationFailure failure,
        string message) => new()
        {
            Failure = failure,
            Message = message
        };
}

public sealed class CreateRequirementRequest
{
    public int ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public RequirementType Type { get; init; }
    public PriorityLevel Priority { get; init; } = PriorityLevel.Medium;
    public string? Rationale { get; init; }
    public string? Preconditions { get; init; }
    public string? ExceptionScenario { get; init; }
    public NfrCategory? NfrCategory { get; init; }
    public IReadOnlyList<int> RelatedFunctionalRequirementIds { get; init; } = [];
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class CreateAiGeneratedRequirementsBatchRequest
{
    public int ProjectId { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
    public IReadOnlyList<CreateRequirementBatchItem> Requirements { get; init; } = [];
}

public sealed class CreateRequirementBatchItem
{
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public RequirementType Type { get; init; }
    public PriorityLevel Priority { get; init; }
    public string? Rationale { get; init; }
    public string? Preconditions { get; init; }
    public string? ExceptionScenario { get; init; }
    public NfrCategory? NfrCategory { get; init; }
    public IReadOnlyList<int> RelatedFunctionalRequirementIds { get; init; } = [];
}

public sealed class RequirementBatchOperationResult
{
    public bool Succeeded { get; init; }
    public RequirementOperationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<int> RequirementIds { get; init; } = [];
}

public sealed class UpdateRequirementRequest
{
    public int ProjectId { get; init; }
    public int RequirementId { get; init; }
    public RequirementStatus ExpectedStatus { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public PriorityLevel Priority { get; init; } = PriorityLevel.Medium;
    public string? Rationale { get; init; }
    public string? Preconditions { get; init; }
    public string? ExceptionScenario { get; init; }
    public NfrCategory? NfrCategory { get; init; }
    public IReadOnlyList<int> RelatedFunctionalRequirementIds { get; init; } = [];
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class TransitionRequirementRequest
{
    public int ProjectId { get; init; }
    public int RequirementId { get; init; }
    public RequirementStatus ExpectedStatus { get; init; }
    public RequirementStatus TargetStatus { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class AddRequirementDependencyRequest
{
    public int ProjectId { get; init; }
    public int RequirementId { get; init; }
    public int DependsOnRequirementId { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class RemoveRequirementDependencyRequest
{
    public int ProjectId { get; init; }
    public int RequirementId { get; init; }
    public int DependencyId { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class AddRequirementTraceRequest
{
    public int ProjectId { get; init; }
    public int RequirementId { get; init; }
    public RequirementTraceStage Stage { get; init; }
    public string ReferenceCode { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class RemoveRequirementTraceRequest
{
    public int ProjectId { get; init; }
    public int RequirementId { get; init; }
    public int TraceId { get; init; }
    public string ActorUserId { get; init; } = string.Empty;
}
