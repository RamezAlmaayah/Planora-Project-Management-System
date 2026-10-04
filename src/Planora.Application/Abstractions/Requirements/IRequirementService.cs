using Planora.Application.Common.Requirements;
using Planora.Domain.Enums;

namespace Planora.Application.Abstractions.Requirements;

public interface IRequirementService
{
    Task<IReadOnlyList<RequirementSummary>> GetProjectRequirementsAsync(
        int projectId,
        string? search = null,
        RequirementFilter? filter = null,
        CancellationToken cancellationToken = default);

    Task<RequirementDetails?> GetByIdAsync(
        int projectId,
        int requirementId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RequirementOption>> GetDependencyOptionsAsync(
        int projectId,
        int excludedRequirementId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RequirementOption>> GetFunctionalRequirementOptionsAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    Task<string> GetIdentifierPreviewAsync(
        int projectId,
        RequirementType type,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RequirementTraceabilityItem>> GetTraceabilityAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    Task<RequirementOperationResult> CreateAsync(
        CreateRequirementRequest request,
        CancellationToken cancellationToken = default);

    Task<RequirementBatchOperationResult> CreateAiGeneratedBatchAsync(
        CreateAiGeneratedRequirementsBatchRequest request,
        CancellationToken cancellationToken = default);

    Task<RequirementOperationResult> UpdateAsync(
        UpdateRequirementRequest request,
        CancellationToken cancellationToken = default);

    Task<RequirementOperationResult> TransitionAsync(
        TransitionRequirementRequest request,
        CancellationToken cancellationToken = default);

    Task<RequirementOperationResult> AddDependencyAsync(
        AddRequirementDependencyRequest request,
        CancellationToken cancellationToken = default);

    Task<RequirementOperationResult> RemoveDependencyAsync(
        RemoveRequirementDependencyRequest request,
        CancellationToken cancellationToken = default);

    Task<RequirementOperationResult> AddTraceAsync(
        AddRequirementTraceRequest request,
        CancellationToken cancellationToken = default);

    Task<RequirementOperationResult> RemoveTraceAsync(
        RemoveRequirementTraceRequest request,
        CancellationToken cancellationToken = default);
}
