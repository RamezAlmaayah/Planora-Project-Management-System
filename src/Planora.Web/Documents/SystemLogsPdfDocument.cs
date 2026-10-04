using Planora.Application.Common.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Planora.Web.Documents;

public sealed class SystemLogsPdfDocument
{
    private readonly IReadOnlyList<SystemLogEntry> _logs;
    private readonly DateTime _fromDate;
    private readonly DateTime _toDate;
    private readonly string _level;
    private readonly string _generatedBy;
    private readonly int _totalCount;

    public SystemLogsPdfDocument(
        IReadOnlyList<SystemLogEntry> logs,
        DateTime fromDate,
        DateTime toDate,
        string level,
        string generatedBy,
        int totalCount = -1)
    {
        _logs = logs;
        _fromDate = fromDate;
        _toDate = toDate;
        _level = level;
        _generatedBy = generatedBy;
        _totalCount = totalCount < 0 ? logs.Count : totalCount;
    }

    public byte[] Generate()
    {
        return Document
            .Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());

                    page.Margin(24);

                    page.DefaultTextStyle(
                        x => x.FontSize(11));

                    page.Header()
                        .Element(ComposeHeader);

                    page.Content()
                        .PaddingVertical(16)
                        .Column(column =>
                        {
                            column.Spacing(16);

                            column.Item()
                                .Element(ComposeReportInfo);

                            column.Item()
                                .Element(ComposeSummary);

                            column.Item()
                                .Element(ComposeLogsTable);
                        });

                    page.Footer()
                        .PaddingTop(10)
                        .BorderTop(1)
                        .BorderColor(Colors.Grey.Lighten2)
                        .Row(row =>
                        {
                            row.RelativeItem()
                                .Text(
                                    "PLANORA • System Log Report")
                                .FontSize(9)
                                .FontColor(
                                    Colors.Grey.Darken1);

                            row.RelativeItem()
                                .AlignRight()
                                .Text(text =>
                                {
                                    text.DefaultTextStyle(
                                        x => x
                                            .FontSize(9)
                                            .FontColor(
                                                Colors.Grey.Darken1));

                                    text.Span("Page ");

                                    text.CurrentPageNumber();

                                    text.Span(" of ");

                                    text.TotalPages();
                                });
                        });
                });
            })
            .GeneratePdf();
    }

    private void ComposeHeader(
        IContainer container)
    {
        container
            .PaddingBottom(14)
            .BorderBottom(1)
            .BorderColor(
                Colors.Grey.Lighten2)
            .Row(row =>
            {
                row.ConstantItem(64)
                    .Height(54)
                    .Background(
                        Colors.Blue.Darken2)
                    .AlignCenter()
                    .AlignMiddle()
                    .Text("P")
                    .FontSize(24)
                    .Bold()
                    .FontColor(
                        Colors.White);

                row.ConstantItem(14);

                row.RelativeItem()
                    .Column(column =>
                    {
                        column.Item()
                            .Text("PLANORA")
                            .FontSize(24)
                            .Bold()
                            .FontColor(
                                Colors.Blue.Darken2);

                        column.Item()
                            .PaddingTop(3)
                            .Text(
                                "System Log Report")
                            .FontSize(16)
                            .SemiBold()
                            .FontColor(
                                Colors.Grey.Darken2);
                    });

                row.ConstantItem(230)
                    .AlignRight()
                    .AlignMiddle()
                    .Column(column =>
                    {
                        column.Item()
                            .AlignRight()
                            .Text("Generated")
                            .FontSize(10)
                            .SemiBold()
                            .FontColor(
                                Colors.Grey.Darken1);

                        column.Item()
                            .PaddingTop(3)
                            .AlignRight()
                            .Text(
                                DateTime.Now.ToString(
                                    "dd MMM yyyy • HH:mm"))
                            .FontSize(11)
                            .FontColor(
                                Colors.Grey.Darken2);
                    });
            });
    }

    private void ComposeReportInfo(
        IContainer container)
    {
        container
            .Background(
                Colors.Grey.Lighten4)
            .Border(1)
            .BorderColor(
                Colors.Grey.Lighten2)
            .Padding(14)
            .Row(row =>
            {
                row.Spacing(20);

                row.RelativeItem()
                    .Column(column =>
                    {
                        column.Item()
                            .Text("Report Period")
                            .FontSize(11)
                            .Bold()
                            .FontColor(
                                Colors.Grey.Darken2);

                        column.Item()
                            .PaddingTop(4)
                            .Text(
                                $"{_fromDate:dd MMM yyyy} → {_toDate:dd MMM yyyy}")
                            .FontSize(11);

                        column.Item()
                            .PaddingTop(4)
                            .Text(_totalCount > _logs.Count
                                ? $"Showing {_logs.Count} most recent of {_totalCount} matching events."
                                : $"Showing all {_logs.Count} matching events.")
                            .FontSize(9)
                            .FontColor(Colors.Grey.Darken1);
                    });

                row.RelativeItem()
                    .Column(column =>
                    {
                        column.Item()
                            .Text("Log Level")
                            .FontSize(11)
                            .Bold()
                            .FontColor(
                                Colors.Grey.Darken2);

                        column.Item()
                            .PaddingTop(4)
                            .Text(_level)
                            .FontSize(11);
                    });

                row.RelativeItem()
                    .Column(column =>
                    {
                        column.Item()
                            .Text("Generated By")
                            .FontSize(11)
                            .Bold()
                            .FontColor(
                                Colors.Grey.Darken2);

                        column.Item()
                            .PaddingTop(4)
                            .Text(_generatedBy)
                            .FontSize(11);
                    });
            });
    }

    private void ComposeSummary(
        IContainer container)
    {
        var information =
            _logs.Count(x =>
                x.Level.Equals(
                    "Information",
                    StringComparison.OrdinalIgnoreCase));

        var warnings =
            _logs.Count(x =>
                x.Level.Equals(
                    "Warning",
                    StringComparison.OrdinalIgnoreCase));

        var errors =
            _logs.Count(x =>
                x.Level.Equals(
                    "Error",
                    StringComparison.OrdinalIgnoreCase));

        var critical =
            _logs.Count(x =>
                x.Level.Equals(
                    "Critical",
                    StringComparison.OrdinalIgnoreCase));

        container.Row(row =>
        {
            row.Spacing(10);

            row.RelativeItem()
                .Element(x =>
                    SummaryCard(
                        x,
                        "Total Events",
                        _logs.Count));

            row.RelativeItem()
                .Element(x =>
                    SummaryCard(
                        x,
                        "Information",
                        information));

            row.RelativeItem()
                .Element(x =>
                    SummaryCard(
                        x,
                        "Warnings",
                        warnings));

            row.RelativeItem()
                .Element(x =>
                    SummaryCard(
                        x,
                        "Errors",
                        errors));

            row.RelativeItem()
                .Element(x =>
                    SummaryCard(
                        x,
                        "Critical",
                        critical));
        });
    }

    private static void SummaryCard(
        IContainer container,
        string title,
        int value)
    {
        container
            .MinHeight(68)
            .Border(1)
            .BorderColor(
                Colors.Grey.Lighten2)
            .Background(
                Colors.Grey.Lighten4)
            .Padding(12)
            .Column(column =>
            {
                column.Item()
                    .Text(title)
                    .FontSize(10)
                    .SemiBold()
                    .FontColor(
                        Colors.Grey.Darken1);

                column.Item()
                    .PaddingTop(5)
                    .Text(value.ToString())
                    .FontSize(19)
                    .Bold()
                    .FontColor(
                        Colors.Blue.Darken2);
            });
    }

    private void ComposeLogsTable(
        IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(112);
                columns.ConstantColumn(82);
                columns.ConstantColumn(145);
                columns.RelativeColumn(3.2f);
                columns.RelativeColumn(2.1f);
            });

            table.Header(header =>
            {
                header.Cell()
                    .Element(TableHeaderCell)
                    .Text("Date & Time");

                header.Cell()
                    .Element(TableHeaderCell)
                    .Text("Level");

                header.Cell()
                    .Element(TableHeaderCell)
                    .Text("Path");

                header.Cell()
                    .Element(TableHeaderCell)
                    .Text("Message");

                header.Cell()
                    .Element(TableHeaderCell)
                    .Text("Trace ID");
            });

            foreach (var log in _logs)
            {
                table.Cell()
                    .Element(TableBodyCell)
                    .Text(
                        log.Timestamp
                            .ToLocalTime()
                            .ToString(
                                "dd/MM/yyyy HH:mm:ss"));

                table.Cell()
                    .Element(TableBodyCell)
                    .Text(log.Level);

                table.Cell()
                    .Element(TableBodyCell)
                    .Text(
                        string.IsNullOrWhiteSpace(
                            log.Path)
                            ? "—"
                            : log.Path);

                table.Cell()
                    .Element(TableBodyCell)
                    .Text(
                        Shorten(
                            log.Message,
                            260));

                table.Cell()
                    .Element(TableBodyCell)
                    .Text(
                        string.IsNullOrWhiteSpace(
                            log.TraceId)
                            ? "—"
                            : log.TraceId);
            }
        });
    }

    private static IContainer TableHeaderCell(
        IContainer container)
    {
        return container
            .Background(
                Colors.Blue.Darken2)
            .PaddingVertical(10)
            .PaddingHorizontal(8)
            .DefaultTextStyle(
                x => x
                    .FontSize(10)
                    .Bold()
                    .FontColor(
                        Colors.White));
    }

    private static IContainer TableBodyCell(
        IContainer container)
    {
        return container
            .BorderBottom(1)
            .BorderColor(
                Colors.Grey.Lighten2)
            .PaddingVertical(9)
            .PaddingHorizontal(8)
            .DefaultTextStyle(
                x => x
                    .FontSize(9.5f)
                    .FontColor(
                        Colors.Grey.Darken3));
    }

    private static string Shorten(
        string? value,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "—";
        }

        var clean =
            value.Replace(
                    Environment.NewLine,
                    " ")
                .Trim();

        if (clean.Length <= maxLength)
        {
            return clean;
        }

        return clean[..maxLength] + "...";
    }
}
