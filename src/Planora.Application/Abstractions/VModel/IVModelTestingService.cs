using Planora.Application.Common.VModelTesting;
namespace Planora.Application.Abstractions.VModel;
public interface IVModelTestingService
{
 Task<TestingProjectOverview?> GetOverviewAsync(int projectId, CancellationToken ct=default);
 Task<TestingOperationResult> EnsurePhasesAsync(int projectId,string actorUserId,CancellationToken ct=default);
 Task<TestingOperationResult> TransitionPhaseAsync(TransitionPhaseRequest request,CancellationToken ct=default);
 Task<IReadOnlyList<VModelTestCaseSummary>> GetTestCasesAsync(int projectId,VModelTestCaseFilter? filter=null,CancellationToken ct=default);
 Task<VModelTestCaseDetails?> GetTestCaseAsync(int projectId,int testCaseId,CancellationToken ct=default);
 Task<IReadOnlyList<ImplementationTestOption>> GetImplementationOptionsAsync(int projectId,CancellationToken ct=default);
 Task<TestingOperationResult> CreateTestCaseAsync(CreateVModelTestCaseRequest request,CancellationToken ct=default);
 Task<TestingOperationResult> UpdateTestCaseAsync(UpdateVModelTestCaseRequest request,CancellationToken ct=default);
 Task<TestingOperationResult> ExecuteAsync(ExecuteVModelTestCaseRequest request,CancellationToken ct=default);
 Task<TestingOperationResult> ValidatePhaseAsync(ValidateVModelPhaseRequest request,CancellationToken ct=default);
 Task<TestingOperationResult> LinkIssueAsync(LinkVModelTestIssueRequest request,CancellationToken ct=default);
}
