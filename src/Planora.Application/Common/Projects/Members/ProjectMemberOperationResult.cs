namespace Planora.Application.Common.Projects.Members;

public sealed class ProjectMemberOperationResult
{
    public bool Succeeded { get; set; }

    public string? ErrorMessage { get; set; }

    public int? MembershipId { get; set; }

    public static ProjectMemberOperationResult Success(
        int membershipId)
    {
        return new ProjectMemberOperationResult
        {
            Succeeded = true,
            MembershipId = membershipId
        };
    }

    public static ProjectMemberOperationResult Failure(
        string errorMessage)
    {
        return new ProjectMemberOperationResult
        {
            Succeeded = false,
            ErrorMessage = errorMessage
        };
    }
}