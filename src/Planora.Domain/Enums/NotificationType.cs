namespace Planora.Domain.Enums;

public enum NotificationType
{
    TaskAssigned = 1,
    TaskSentToQa = 2,
    QaPassed = 3,
    QaFailed = 4,
    IssueAssigned = 5,
    SprintStarted = 6,
    SprintCompleted = 7,
    MemberAdded = 8,
    RequirementUpdated = 9,
    IssueResolved = 10,
    IssueReopened = 11,
    IssueClosed = 12
}
