using System.Collections.Concurrent;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Srs;
using Planora.Application.Common.Srs;

namespace Planora.Infrastructure.Gemini;

public sealed class InMemorySrsDraftCache : ISrsDraftCache
{
    private static readonly TimeSpan TimeToLive = TimeSpan.FromMinutes(20);
    private readonly ConcurrentDictionary<DraftKey, CachedDraft> _drafts = new();
    private readonly IClock _clock;

    public InMemorySrsDraftCache(IClock clock) => _clock = clock;

    public string Store(SrsDraft draft)
    {
        string token = Convert.ToHexString(Guid.NewGuid().ToByteArray());
        var stored = new SrsDraft
        {
            Token = token,
            ProjectId = draft.ProjectId,
            ProjectName = draft.ProjectName,
            OwnerUserId = draft.OwnerUserId,
            Content = draft.Content,
            QualityScore = draft.QualityScore,
            QualityLevel = draft.QualityLevel,
            GeneratedByUserId = draft.GeneratedByUserId,
            GeneratedAt = draft.GeneratedAt
        };
        _drafts[new DraftKey(draft.OwnerUserId, draft.ProjectId, token)] =
            new CachedDraft(stored, _clock.UtcNow.Add(TimeToLive));
        RemoveExpired();
        return token;
    }

    public SrsDraft? Get(string ownerUserId, int projectId, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var key = new DraftKey(ownerUserId, projectId, token);
        if (!_drafts.TryGetValue(key, out CachedDraft? cached)) return null;
        if (cached.ExpiresAt > _clock.UtcNow) return cached.Draft;
        _drafts.TryRemove(key, out _);
        return null;
    }

    public SrsDraft? Take(string ownerUserId, int projectId, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var key = new DraftKey(ownerUserId, projectId, token);
        if (!_drafts.TryRemove(key, out CachedDraft? cached)) return null;
        return cached.ExpiresAt > _clock.UtcNow ? cached.Draft : null;
    }

    public void Restore(SrsDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.Token) ||
            string.IsNullOrWhiteSpace(draft.OwnerUserId) ||
            draft.ProjectId <= 0)
            return;

        _drafts[new DraftKey(draft.OwnerUserId, draft.ProjectId, draft.Token)] =
            new CachedDraft(draft, _clock.UtcNow.Add(TimeToLive));
    }

    private void RemoveExpired()
    {
        foreach ((DraftKey key, CachedDraft value) in _drafts)
            if (value.ExpiresAt <= _clock.UtcNow) _drafts.TryRemove(key, out _);
    }

    private sealed record DraftKey(string OwnerUserId, int ProjectId, string Token);
    private sealed record CachedDraft(SrsDraft Draft, DateTime ExpiresAt);
}
