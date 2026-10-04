namespace Planora.Application.Abstractions.Common;

public sealed record EmailInlineResource(
    string ContentId,
    string MediaType,
    byte[] Content);
