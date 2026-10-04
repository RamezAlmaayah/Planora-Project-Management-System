namespace Planora.Application.Common.Tasks;

public sealed class TaskAssignmentValidationResult
{
    public bool Succeeded { get; set; }

    public string? ErrorMessage { get; set; }

    public int? ProjectId { get; set; }

    public static TaskAssignmentValidationResult Success(
        int projectId)
    {
        return new TaskAssignmentValidationResult
        {
            Succeeded = true,
            ProjectId = projectId
        };
    }

    public static TaskAssignmentValidationResult Failure(
        string errorMessage)
    {
        return new TaskAssignmentValidationResult
        {
            Succeeded = false,
            ErrorMessage = errorMessage
        };
    }
}