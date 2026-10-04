using Planora.Application.Common.Srs;

namespace Planora.Application.Abstractions.Srs;

public interface ISrsService
{
    bool IsConfigured { get; }
    Task<SrsGenerationResult> AnalyzeInputAsync(GenerateSrsRequest request, CancellationToken cancellationToken = default);
    Task<SrsGenerationResult> GenerateAsync(GenerateSrsRequest request, CancellationToken cancellationToken = default);
    Task<SrsDraftResult> GetDraftAsync(int projectId, string token, string actorUserId, CancellationToken cancellationToken = default);
    Task<SrsSaveResult> SaveAsync(SaveSrsRequest request, CancellationToken cancellationToken = default);
    Task<SrsListResult> GetDocumentsAsync(int projectId, string actorUserId, CancellationToken cancellationToken = default);
    Task<SrsDocumentResult> GetDocumentAsync(int projectId, int documentId, string actorUserId, CancellationToken cancellationToken = default);
    Task<SrsOperationResult> RecordExportAsync(int projectId, int documentId, string actorUserId, string format, CancellationToken cancellationToken = default);
}
