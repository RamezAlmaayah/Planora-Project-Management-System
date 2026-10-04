using Planora.Application.Common.Srs;

namespace Planora.Application.Abstractions.Srs;

public interface ISrsDraftCache
{
    string Store(SrsDraft draft);
    SrsDraft? Get(string ownerUserId, int projectId, string token);
    SrsDraft? Take(string ownerUserId, int projectId, string token);
    void Restore(SrsDraft draft);
}
