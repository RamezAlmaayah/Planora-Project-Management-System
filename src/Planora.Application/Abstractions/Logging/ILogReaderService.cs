using Planora.Application.Common.Logging;

namespace Planora.Application.Abstractions.Logging;

public interface ILogReaderService
{
    Task<SystemLogPage> GetLogsPageAsync(
        DateTime fromDate,
        DateTime toDate,
        string? level,
        int page,
        int pageSize = 50,
        CancellationToken cancellationToken = default);
}
