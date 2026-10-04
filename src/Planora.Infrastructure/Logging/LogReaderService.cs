using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Planora.Application.Abstractions.Logging;
using Planora.Application.Common.Logging;

namespace Planora.Infrastructure.Logging;

public sealed partial class LogReaderService
    : ILogReaderService
{
    private readonly IHostEnvironment _environment;

    public LogReaderService(
        IHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<SystemLogPage> GetLogsPageAsync(
        DateTime fromDate,
        DateTime toDate,
        string? level,
        int page,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var from = fromDate.Date;
        var to = toDate.Date.AddDays(1).AddTicks(-1);
        if (to < from)
            return new SystemLogPage { Page = 1, PageSize = pageSize };

        var files = Directory.Exists(Path.Combine(_environment.ContentRootPath, "logs"))
            ? Directory.GetFiles(Path.Combine(_environment.ContentRootPath, "logs"), "planora-*.log")
                .Where(file => IsFileInDateRange(file, from, to))
                .OrderBy(file => file, StringComparer.Ordinal)
                .ToArray()
            : [];

        int total = 0;
        foreach (var file in files)
            await ScanFileAsync(file, from, to, level, cancellationToken,
                entry => { total++; return ValueTask.CompletedTask; });

        int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        int currentPage = Math.Clamp(page, 1, totalPages);
        int newestStart = Math.Max(0, total - currentPage * pageSize);
        int newestEnd = total - (currentPage - 1) * pageSize;
        var selected = new List<SystemLogEntry>(Math.Min(pageSize, total));
        int index = 0;
        foreach (var file in files)
            await ScanFileAsync(file, from, to, level, cancellationToken, entry =>
            {
                if (index >= newestStart && index < newestEnd)
                    selected.Add(entry);
                index++;
                return ValueTask.CompletedTask;
            });
        selected.Reverse();
        return new SystemLogPage
        {
            Items = selected,
            Page = currentPage,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    private static bool IsFileInDateRange(string path, DateTime from, DateTime to)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        string date = name.StartsWith("planora-", StringComparison.OrdinalIgnoreCase)
            ? name[8..] : string.Empty;
        return !DateTime.TryParseExact(date, "yyyyMMdd", CultureInfo.InvariantCulture,
                   DateTimeStyles.None, out var fileDate) ||
               fileDate.Date >= from.Date && fileDate.Date <= to.Date;
    }

    private static async Task ScanFileAsync(
        string filePath, DateTime from, DateTime to, string? level,
        CancellationToken cancellationToken,
        Func<SystemLogEntry, ValueTask> emit)
    {
        SystemLogEntry? current = null;
        var details = new StringBuilder();
        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var reader = new StreamReader(stream);
        async ValueTask FlushAsync()
        {
            if (current is null) return;
            current.Details = NormalizeDetails(details);
            details.Clear();
            if (current.Timestamp.DateTime >= from && current.Timestamp.DateTime <= to &&
                (string.IsNullOrWhiteSpace(level) || level.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                 current.Level.Equals(level, StringComparison.OrdinalIgnoreCase)))
                await emit(current);
            current = null;
        }
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var match = LogLineRegex().Match(line);
            if (!match.Success)
            {
                if (current is not null) details.AppendLine(line);
                continue;
            }
            await FlushAsync();
            if (!DateTimeOffset.TryParseExact(match.Groups["timestamp"].Value,
                    "yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var timestamp)) continue;
            string message = match.Groups["message"].Value;
            current = new SystemLogEntry
            {
                Timestamp = timestamp,
                Level = ConvertLevel(match.Groups["level"].Value),
                Message = message,
                Path = ExtractPath(message),
                TraceId = ExtractTraceId(message)
            };
        }
        await FlushAsync();
    }

    private static string? NormalizeDetails(
        StringBuilder builder)
    {
        if (builder.Length == 0)
        {
            return null;
        }

        var value =
            builder.ToString().Trim();

        return string.IsNullOrWhiteSpace(value)
            ? null
            : value;
    }

    private static string ConvertLevel(
        string level)
    {
        return level switch
        {
            "INF" => "Information",
            "WRN" => "Warning",
            "ERR" => "Error",
            "FTL" => "Critical",
            "DBG" => "Debug",
            "VRB" => "Verbose",
            _ => level
        };
    }

    private static string? ExtractTraceId(
        string message)
    {
        var match =
            TraceIdRegex().Match(message);

        return match.Success
            ? match.Groups["traceId"].Value
            : null;
    }

    private static string? ExtractPath(
        string message)
    {
        var explicitPath =
            ExplicitPathRegex().Match(message);

        if (explicitPath.Success)
        {
            return explicitPath
                .Groups["path"]
                .Value;
        }

        var httpPath =
            HttpRequestRegex().Match(message);

        if (httpPath.Success)
        {
            return httpPath
                .Groups["path"]
                .Value;
        }

        return null;
    }

    [GeneratedRegex(
        @"^(?<timestamp>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) \[(?<level>[A-Z]{3})\] (?<message>.*)$")]
    private static partial Regex LogLineRegex();

    [GeneratedRegex(
        @"TraceId:\s*(?<traceId>[^,\s]+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex TraceIdRegex();

    [GeneratedRegex(
        @"Path:\s*(?<path>[^,\s]+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitPathRegex();

    [GeneratedRegex(
        @"^HTTP\s+\S+\s+(?<path>\S+)\s+responded",
        RegexOptions.IgnoreCase)]
    private static partial Regex HttpRequestRegex();
}
