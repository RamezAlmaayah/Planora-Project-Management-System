using Planora.Domain.Enums;

namespace Planora.Application.Common.VModel;

public sealed class RequirementArtifactOption
{
    public int Id { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public RequirementStatus Status { get; init; }
}

public sealed class DesignArtifactOption
{
    public int Id { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
}

public class DesignArtifactSummary
{
    public int Id { get; init; }
    public int ProjectId { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public DesignArtifactType Type { get; init; }
    public int LinkedRequirementCount { get; init; }
    public string CreatorName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class DesignArtifactDetails : DesignArtifactSummary
{
    public string ProjectName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? UpdatedByName { get; init; }
    public IReadOnlyList<RequirementArtifactOption> Requirements { get; set; } = [];
    public IReadOnlyList<ImplementationArtifactSummary> ImplementationArtifacts { get; set; } = [];
}

public class ImplementationArtifactSummary
{
    public int Id { get; init; }
    public int ProjectId { get; init; }
    public string Identifier { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public ImplementationArtifactType Type { get; init; }
    public string? SourceReference { get; init; }
    public int LinkedDesignCount { get; init; }
    public string CreatorUserId { get; init; } = string.Empty;
    public string CreatorName { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}

public sealed class ImplementationArtifactDetails : ImplementationArtifactSummary
{
    public string ProjectName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? UpdatedByName { get; init; }
    public IReadOnlyList<DesignArtifactOption> Designs { get; set; } = [];
    public IReadOnlyList<RequirementArtifactOption> DerivedRequirements { get; set; } = [];
}

public sealed class CreateDesignArtifactRequest
{
    public int ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DesignArtifactType Type { get; init; }
    public IReadOnlyList<int> RequirementIds { get; init; } = [];
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class UpdateDesignArtifactRequest
{
    public int ProjectId { get; init; }
    public int ArtifactId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DesignArtifactType Type { get; init; }
    public IReadOnlyList<int> RequirementIds { get; init; } = [];
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class CreateImplementationArtifactRequest
{
    public int ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ImplementationArtifactType Type { get; init; }
    public string? SourceReference { get; init; }
    public IReadOnlyList<int> DesignArtifactIds { get; init; } = [];
    public string ActorUserId { get; init; } = string.Empty;
}

public sealed class UpdateImplementationArtifactRequest
{
    public int ProjectId { get; init; }
    public int ArtifactId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public ImplementationArtifactType Type { get; init; }
    public string? SourceReference { get; init; }
    public IReadOnlyList<int> DesignArtifactIds { get; init; } = [];
    public string ActorUserId { get; init; } = string.Empty;
}


public enum ArtifactOperationFailure
{
    None = 0,
    Validation,
    NotFound,
    Forbidden,
    ReadOnly,
    Conflict
}

public sealed class ArtifactOperationResult
{
    public bool Succeeded { get; init; }
    public int? ArtifactId { get; init; }
    public string? Identifier { get; init; }
    public ArtifactOperationFailure Failure { get; init; }
    public string Message { get; init; } = string.Empty;

    public static ArtifactOperationResult Success(
        int artifactId, string identifier, string message) => new()
        {
            Succeeded = true,
            ArtifactId = artifactId,
            Identifier = identifier,
            Message = message
        };

    public static ArtifactOperationResult Failed(
        ArtifactOperationFailure failure, string message) => new()
        {
            Failure = failure,
            Message = message
        };
}
