using System.Collections.Concurrent;
using Planora.Application.Abstractions.Ai;
using Planora.Application.Abstractions.Common;
using Planora.Application.Common.Ai;

namespace Planora.Infrastructure.Gemini;

public sealed class InMemoryAiInputQualityCache : IAiInputQualityCache
{
    private static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<CacheKey, AiInputQualityCacheRecord> _entries = new();
    private readonly IClock _clock;

    public InMemoryAiInputQualityCache(IClock clock) => _clock = clock;

    public AiInputQualityCacheRecord? Get(string userId, int projectId, string inputHash)
    {
        var key = new CacheKey(userId, projectId, inputHash);
        if (!_entries.TryGetValue(key, out AiInputQualityCacheRecord? record)) return null;
        if (_clock.UtcNow - record.AnalyzedAt <= TimeToLive) return record;
        _entries.TryRemove(key, out _);
        return null;
    }

    public void Set(string userId, int projectId, string inputHash, AiInputQualityCacheRecord record) =>
        _entries[new CacheKey(userId, projectId, inputHash)] = record;

    private sealed record CacheKey(string UserId, int ProjectId, string InputHash);
}
