using Planora.Application.Common.Ai;

namespace Planora.Application.Abstractions.Ai;

public interface IAiRequirementGenerationService
{
    bool IsConfigured { get; }

    Task<AiInputQualityAnalysisResult> AnalyzeInputQualityAsync(
        AnalyzeAiRequirementInputRequest request,
        CancellationToken cancellationToken = default);

    Task<AiInputQualityAnalysisResult> EnsureInputQualityAsync(
        AnalyzeAiRequirementInputRequest request,
        CancellationToken cancellationToken = default);

    Task<AiRequirementGenerationResult> GenerateAsync(
        GenerateAiRequirementsRequest request,
        CancellationToken cancellationToken = default);

    Task<AiRequirementSaveResult> SaveSelectedAsync(
        SaveAiRequirementsRequest request,
        CancellationToken cancellationToken = default);
}
