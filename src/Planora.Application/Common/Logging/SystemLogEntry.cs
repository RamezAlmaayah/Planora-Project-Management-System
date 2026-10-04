namespace Planora.Application.Common.Logging;

public sealed class SystemLogEntry
{
    public DateTimeOffset Timestamp { get; set; }

    public string Level { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Details { get; set; }

    public string? Path { get; set; }

    public string? TraceId { get; set; }
}

public sealed class SystemLogPage
{
    public IReadOnlyList<SystemLogEntry> Items { get; init; } = Array.Empty<SystemLogEntry>();
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
}
