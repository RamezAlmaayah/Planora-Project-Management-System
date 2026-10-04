using Planora.Application.Common.VModel;

namespace Planora.Application.Abstractions.VModel;

public interface IVModelArtifactService
{
    Task<IReadOnlyList<DesignArtifactSummary>> GetDesignArtifactsAsync(
        int projectId, string? search = null,
        CancellationToken cancellationToken = default);
    Task<DesignArtifactDetails?> GetDesignArtifactAsync(
        int projectId, int artifactId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequirementArtifactOption>> GetApprovedRequirementOptionsAsync(
        int projectId, int? designArtifactId = null,
        CancellationToken cancellationToken = default);
    Task<string> GetNextDesignIdentifierPreviewAsync(
        int projectId, CancellationToken cancellationToken = default);
    Task<ArtifactOperationResult> CreateDesignArtifactAsync(
        CreateDesignArtifactRequest request,
        CancellationToken cancellationToken = default);
    Task<ArtifactOperationResult> UpdateDesignArtifactAsync(
        UpdateDesignArtifactRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImplementationArtifactSummary>> GetImplementationArtifactsAsync(
        int projectId, string? search = null,
        CancellationToken cancellationToken = default);
    Task<ImplementationArtifactDetails?> GetImplementationArtifactAsync(
        int projectId, int artifactId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DesignArtifactOption>> GetDesignArtifactOptionsAsync(
        int projectId, CancellationToken cancellationToken = default);
    Task<string> GetNextImplementationIdentifierPreviewAsync(
        int projectId, CancellationToken cancellationToken = default);
    Task<ArtifactOperationResult> CreateImplementationArtifactAsync(
        CreateImplementationArtifactRequest request,
        CancellationToken cancellationToken = default);
    Task<ArtifactOperationResult> UpdateImplementationArtifactAsync(
        UpdateImplementationArtifactRequest request,
        CancellationToken cancellationToken = default);
}
