namespace Planora.Application.Common.Scrum.Backlog;

public sealed class BacklogOperationResult
{
    public bool Succeeded { get; init; }

    public string? Error { get; init; }

    public BacklogItemSummary? Item { get; init; }

    public static BacklogOperationResult Success(
        BacklogItemSummary item)
    {
        return new BacklogOperationResult
        {
            Succeeded = true,
            Item = item
        };
    }

    public static BacklogOperationResult Failure(
        string error)
    {
        return new BacklogOperationResult
        {
            Succeeded = false,
            Error = error
        };
    }
}
