using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Planora.Application.Abstractions.Logging;
using Planora.Application.Common.Security;
using Planora.Web.Documents;
using Planora.Web.ViewModels.AdminLogs;

namespace Planora.Web.Controllers;

[Authorize(
    Policy = AuthorizationPolicies.AdminOnly)]
[Route("Admin/SystemLogs")]
public sealed class AdminLogsController : Controller
{
    private static readonly string[] AllowedLevels =
    [
        "All",
        "Information",
        "Warning",
        "Error",
        "Critical"
    ];

    private readonly ILogReaderService _logReaderService;
    private readonly ILogger<AdminLogsController> _logger;

    public AdminLogsController(
        ILogReaderService logReaderService,
        ILogger<AdminLogsController> logger)
    {
        _logReaderService = logReaderService;
        _logger = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(
        DateTime? fromDate,
        DateTime? toDate,
        string? level,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        var today =
            DateTime.Today;

        var selectedFromDate =
            fromDate?.Date
            ?? today.AddDays(-6);

        var selectedToDate =
            toDate?.Date
            ?? today;

        var selectedLevel =
            NormalizeLevel(level);

        var model =
            new SystemLogsViewModel
            {
                FromDate =
                    selectedFromDate,

                ToDate =
                    selectedToDate,

                Level =
                    selectedLevel
            };

        if (selectedFromDate
            > selectedToDate)
        {
            ModelState.AddModelError(
                string.Empty,
                "From Date cannot be after To Date.");

            return View(model);
        }

        var result = await _logReaderService.GetLogsPageAsync(
                    selectedFromDate,
                    selectedToDate,
                    selectedLevel,
                    page,
                    cancellationToken: cancellationToken);
        model.Logs = result.Items;
        model.Page = result.Page;
        model.PageSize = result.PageSize;
        model.TotalCount = result.TotalCount;
        model.TotalPages = result.TotalPages;

        _logger.LogInformation(
            "Admin viewed system logs from {FromDate} to {ToDate} with level {Level}.",
            selectedFromDate,
            selectedToDate,
            selectedLevel);

        return View(model);
    }

    [HttpGet("DownloadPdf")]
    public async Task<IActionResult> DownloadPdf(
        DateTime? fromDate,
        DateTime? toDate,
        string? level,
        CancellationToken cancellationToken)
    {
        var today =
            DateTime.Today;

        var selectedFromDate =
            fromDate?.Date
            ?? today.AddDays(-6);

        var selectedToDate =
            toDate?.Date
            ?? today;

        var selectedLevel =
            NormalizeLevel(level);

        if (selectedFromDate
            > selectedToDate)
        {
            return BadRequest(
                "From Date cannot be after To Date.");
        }

        var logPage =
            await _logReaderService
                .GetLogsPageAsync(
                    selectedFromDate,
                    selectedToDate,
                    selectedLevel,
                    page: 1,
                    pageSize: 100,
                    cancellationToken: cancellationToken);

        var generatedBy =
            User.Identity?.Name
            ?? "Planora Administrator";

        var document =
            new SystemLogsPdfDocument(
                logPage.Items,
                selectedFromDate,
                selectedToDate,
                selectedLevel,
                generatedBy,
                logPage.TotalCount);

        var pdfBytes =
            document.Generate();

        _logger.LogInformation(
            "Admin generated system logs PDF from {FromDate} to {ToDate} with level {Level}.",
            selectedFromDate,
            selectedToDate,
            selectedLevel);

        var fileName =
            $"Planora-System-Logs-{selectedFromDate:yyyyMMdd}-{selectedToDate:yyyyMMdd}.pdf";

        return File(
            pdfBytes,
            "application/pdf",
            fileName);
    }

    private static string NormalizeLevel(
        string? level)
    {
        if (string.IsNullOrWhiteSpace(
            level))
        {
            return "All";
        }

        var match =
            AllowedLevels
                .FirstOrDefault(
                    x => x.Equals(
                        level,
                        StringComparison.OrdinalIgnoreCase));

        return match ?? "All";
    }
}
