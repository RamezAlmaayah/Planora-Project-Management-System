using Planora.Application.Common.Ai;

namespace Planora.Application.Abstractions.Ai;

public interface IAiInputQualityCache
{
    AiInputQualityCacheRecord? Get(string userId, int projectId, string inputHash);
    void Set(string userId, int projectId, string inputHash, AiInputQualityCacheRecord record);
}
