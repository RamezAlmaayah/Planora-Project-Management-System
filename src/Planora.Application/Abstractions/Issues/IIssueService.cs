using Planora.Application.Common.Issues;

namespace Planora.Application.Abstractions.Issues;

public interface IIssueService
{
    Task<IReadOnlyList<IssueSummary>> GetProjectIssuesAsync(
        int projectId,
        string? search = null,
        IssueFilter? filter = null,
        CancellationToken cancellationToken = default);

    Task<IssueDetails?> GetByIdAsync(
        int projectId,
        int issueId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueTaskOption>> GetTaskOptionsAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IssueAssigneeOption>> GetDeveloperOptionsAsync(
        int projectId,
        CancellationToken cancellationToken = default);

    Task<IssueOperationResult> CreateAsync(
        CreateIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<IssueOperationResult> UpdateAsync(
        UpdateIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<IssueOperationResult> AssignAsync(
        AssignIssueRequest request,
        CancellationToken cancellationToken = default);

    Task<IssueOperationResult> TransitionAsync(
        TransitionIssueRequest request,
        CancellationToken cancellationToken = default);
}
