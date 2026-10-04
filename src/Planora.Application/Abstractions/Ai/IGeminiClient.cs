using Planora.Application.Common.Ai;

namespace Planora.Application.Abstractions.Ai;

public interface IGeminiClient
{
    bool IsConfigured { get; }

    Task<GeminiClientResult> GenerateJsonAsync(
        GeminiClientRequest request,
        CancellationToken cancellationToken = default);
}
