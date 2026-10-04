namespace Planora.Application.Common.Scrum.Sprints;

public sealed class CreateSprintRequest
{
    public int ProjectId { get; init; }

    public string Name { get; init; } = string.Empty;
    public string Goal { get; init; } = string.Empty;

    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }

    public string CreatedByUserId { get; init; } = string.Empty;
}
