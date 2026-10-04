using System.ComponentModel.DataAnnotations;
using Planora.Application.Common.Logging;

namespace Planora.Web.ViewModels.AdminLogs;

public sealed class SystemLogsViewModel
{
    [DataType(DataType.Date)]
    [Display(Name = "From Date")]
    public DateTime FromDate { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "To Date")]
    public DateTime ToDate { get; set; }

    public string Level { get; set; } = "All";

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;

    public int TotalCount { get; set; }

    public int TotalPages { get; set; } = 1;

    public IReadOnlyList<SystemLogEntry> Logs { get; set; }
        = Array.Empty<SystemLogEntry>();

    public int TotalEvents => TotalCount;

    public int InformationCount =>
        Logs.Count(x =>
            x.Level.Equals(
                "Information",
                StringComparison.OrdinalIgnoreCase));

    public int WarningCount =>
        Logs.Count(x =>
            x.Level.Equals(
                "Warning",
                StringComparison.OrdinalIgnoreCase));

    public int ErrorCount =>
        Logs.Count(x =>
            x.Level.Equals(
                "Error",
                StringComparison.OrdinalIgnoreCase));

    public int CriticalCount =>
        Logs.Count(x =>
            x.Level.Equals(
                "Critical",
                StringComparison.OrdinalIgnoreCase));
}
