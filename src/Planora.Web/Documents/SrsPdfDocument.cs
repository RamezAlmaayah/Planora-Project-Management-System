using System.Globalization;
using Planora.Application.Common.Srs;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Planora.Web.Documents;

public sealed class SrsPdfDocument
{
    private readonly SrsDocumentDetails _document;

    public SrsPdfDocument(SrsDocumentDetails document) => _document = document;

    public byte[] Generate() => Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(36);
            page.DefaultTextStyle(style => style.FontSize(10.5f).FontColor(Colors.Grey.Darken3));
            page.Background().PaddingTop(36).PaddingHorizontal(36).Element(ComposeHeader);
            page.Content().PaddingTop(78).PaddingBottom(18).Column(ComposeContent);
            page.Footer().PaddingTop(10).BorderTop(1).BorderColor(Colors.Grey.Lighten2).Row(row =>
            {
                row.RelativeItem().Text("PLANORA - IEEE-style Software Requirements Specification")
                    .FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                row.RelativeItem().AlignRight().Text(text =>
                {
                    text.DefaultTextStyle(style => style.FontSize(8.5f).FontColor(Colors.Grey.Darken1));
                    text.Span("Page "); text.CurrentPageNumber(); text.Span(" of "); text.TotalPages();
                });
            });
        });
    }).GeneratePdf();

    private void ComposeHeader(IContainer container)
    {
        container.PaddingBottom(16).BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Row(row =>
        {
            row.ConstantItem(52).Height(44).Background(Colors.Blue.Darken2)
                .AlignCenter().AlignMiddle().Text("P").FontSize(21).Bold().FontColor(Colors.White);
            row.ConstantItem(14);
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("PLANORA").FontSize(20).Bold().FontColor(Colors.Blue.Darken2);
                column.Item().Text("Software Requirements Specification").FontSize(12).SemiBold();
            });
            row.ConstantItem(145).AlignRight().Column(column =>
            {
                column.Item().AlignRight().Text(_document.ProjectName).FontSize(10).SemiBold();
                column.Item().PaddingTop(3).AlignRight().Text(
                        $"Saved {_document.SavedAt.ToString("dd MMM yyyy", CultureInfo.InvariantCulture)}")
                    .FontSize(9).FontColor(Colors.Grey.Darken1);
            });
        });
    }

    private void ComposeContent(ColumnDescriptor column)
    {
        column.Spacing(14);
        column.Item().Text(_document.Content.DocumentTitle).FontSize(22).Bold().FontColor(Colors.Blue.Darken3);
        column.Item().Background(Colors.Grey.Lighten4).Border(1).BorderColor(Colors.Grey.Lighten2)
            .Padding(12).Text($"AI Input Quality Summary: {_document.QualityScore}/100 ({_document.QualityLevel})")
            .FontSize(9.5f).SemiBold();
        TextSection(column, "1. Introduction",
            ("Purpose", _document.Content.Introduction.Purpose),
            ("Scope", _document.Content.Introduction.Scope),
            ("Document Overview", _document.Content.Introduction.DocumentOverview));
        TextSection(column, "2. Overall Description",
            ("Product Perspective", _document.Content.OverallDescription.ProductPerspective),
            ("Product Functions", _document.Content.OverallDescription.ProductFunctions),
            ("User / Actor Overview", _document.Content.OverallDescription.UserActorOverview),
            ("Operating Environment", _document.Content.OverallDescription.OperatingEnvironment));
        Heading(column, "3. Actors / User Roles");
        if (_document.Content.Actors.Count == 0) Paragraph(column, "Not specified.");
        foreach (SrsActor actor in _document.Content.Actors)
            LabeledParagraph(column, actor.Name, actor.Description);
        Heading(column, "4. Functional Requirements");
        if (_document.Content.FunctionalRequirements.Count == 0) Paragraph(column, "Not specified.");
        foreach (SrsFunctionalRequirement requirement in _document.Content.FunctionalRequirements)
            RequirementCard(column, requirement.Identifier, requirement.Name,
                ("Priority", requirement.Priority), ("Description", requirement.Description),
                ("Business Rationale", requirement.BusinessRationale),
                ("Preconditions", requirement.Preconditions),
                ("Exception Scenario", requirement.ExceptionScenario));
        Heading(column, "5. Non-Functional Requirements");
        if (_document.Content.NonFunctionalRequirements.Count == 0) Paragraph(column, "Not specified.");
        foreach (SrsNonFunctionalRequirement requirement in _document.Content.NonFunctionalRequirements)
            RequirementCard(column, requirement.Identifier, requirement.Category,
                ("Priority", requirement.Priority), ("Description", requirement.Description),
                ("Rationale", requirement.Rationale),
                ("Related Functional Requirements", requirement.RelatedFunctionalRequirements.Count == 0
                    ? "Not specified." : string.Join(", ", requirement.RelatedFunctionalRequirements)));
        ListSection(column, "6. External Interface Requirements", _document.Content.ExternalInterfaceRequirements);
        ListSection(column, "7. Data Requirements", _document.Content.DataRequirements);
        ListSection(column, "8. Constraints", _document.Content.Constraints);
        ListSection(column, "9. Assumptions and Dependencies", _document.Content.AssumptionsAndDependencies);
        ListSection(column, "10. Acceptance Criteria", _document.Content.AcceptanceCriteria);
        Heading(column, "11. AI Input Quality Summary");
        Paragraph(column, _document.Content.AiQualitySummary);
    }

    private static void TextSection(ColumnDescriptor column, string heading, params (string Label, string Text)[] fields)
    {
        Heading(column, heading);
        foreach ((string label, string text) in fields) LabeledParagraph(column, label, text);
    }
    private static void ListSection(ColumnDescriptor column, string heading, IReadOnlyList<string> items)
    {
        Heading(column, heading);
        if (items.Count == 0) { Paragraph(column, "Not specified."); return; }
        foreach (string item in items)
            column.Item().PaddingLeft(8).Text(text => { text.Span("- ").SemiBold(); text.Span(item); });
    }
    private static void Heading(ColumnDescriptor column, string heading) =>
        column.Item().PaddingTop(7).Text(heading).FontSize(14).Bold().FontColor(Colors.Blue.Darken2);
    private static void Paragraph(ColumnDescriptor column, string text) =>
        column.Item().Text(text).LineHeight(1.35f);
    private static void LabeledParagraph(ColumnDescriptor column, string label, string text) =>
        column.Item().DefaultTextStyle(style => style.LineHeight(1.35f))
            .Text(value => { value.Span($"{label}: ").SemiBold(); value.Span(text); });
    private static void RequirementCard(
        ColumnDescriptor column, string identifier, string title, params (string Label, string Text)[] fields)
    {
        column.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(card =>
        {
            card.Spacing(6);
            card.Item().Text(text =>
            {
                text.Span(identifier).Bold().FontColor(Colors.Blue.Darken2);
                text.Span($"  {title}").SemiBold();
            });
            foreach ((string label, string text) in fields)
                card.Item().DefaultTextStyle(style => style.FontSize(9.5f).LineHeight(1.3f))
                    .Text(value => { value.Span($"{label}: ").SemiBold(); value.Span(text); });
        });
    }
}
