namespace Planora.Application.Common.Scrum.Sprints;

public sealed class SprintOperationResult
{
    public bool Succeeded { get; init; }

    public string? Error { get; init; }

    public SprintSummary? Sprint { get; init; }

    public static SprintOperationResult Success(
        SprintSummary? sprint = null)
        => new()
        {
            Succeeded = true,
            Sprint = sprint
        };

    public static SprintOperationResult Failure(
        string error)
        => new()
        {
            Succeeded = false,
            Error = error
        };
}
