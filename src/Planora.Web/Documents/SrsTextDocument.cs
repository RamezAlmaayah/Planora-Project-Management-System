using System.Text;
using System.Globalization;
using Planora.Application.Common.Srs;

namespace Planora.Web.Documents;

public static class SrsTextDocument
{
    public static string Render(SrsDocumentDetails document)
    {
        var text = new StringBuilder();
        text.AppendLine("PLANORA");
        text.AppendLine("SOFTWARE REQUIREMENTS SPECIFICATION");
        text.AppendLine();
        text.AppendLine($"Project: {document.ProjectName}");
        text.AppendLine($"Document: {document.Title}");
        text.AppendLine($"Saved: {document.SavedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} UTC");
        text.AppendLine($"AI Input Quality: {document.QualityScore}/100 ({document.QualityLevel})");
        text.AppendLine();
        Section(text, "1. Introduction");
        Field(text, "Purpose", document.Content.Introduction.Purpose);
        Field(text, "Scope", document.Content.Introduction.Scope);
        Field(text, "Document Overview", document.Content.Introduction.DocumentOverview);
        Section(text, "2. Overall Description");
        Field(text, "Product Perspective", document.Content.OverallDescription.ProductPerspective);
        Field(text, "Product Functions", document.Content.OverallDescription.ProductFunctions);
        Field(text, "User / Actor Overview", document.Content.OverallDescription.UserActorOverview);
        Field(text, "Operating Environment", document.Content.OverallDescription.OperatingEnvironment);
        Section(text, "3. Actors / User Roles");
        foreach (SrsActor actor in document.Content.Actors)
            Field(text, actor.Name, actor.Description);
        if (document.Content.Actors.Count == 0) text.AppendLine("Not specified.").AppendLine();
        Section(text, "4. Functional Requirements");
        foreach (SrsFunctionalRequirement requirement in document.Content.FunctionalRequirements)
        {
            text.AppendLine($"{requirement.Identifier} - {requirement.Name}");
            Field(text, "Priority", requirement.Priority);
            Field(text, "Description", requirement.Description);
            Field(text, "Business Rationale", requirement.BusinessRationale);
            Field(text, "Preconditions", requirement.Preconditions);
            Field(text, "Exception Scenario", requirement.ExceptionScenario);
        }
        if (document.Content.FunctionalRequirements.Count == 0) text.AppendLine("Not specified.").AppendLine();
        Section(text, "5. Non-Functional Requirements");
        foreach (SrsNonFunctionalRequirement requirement in document.Content.NonFunctionalRequirements)
        {
            text.AppendLine($"{requirement.Identifier} - {requirement.Category}");
            Field(text, "Priority", requirement.Priority);
            Field(text, "Description", requirement.Description);
            Field(text, "Rationale", requirement.Rationale);
            Field(text, "Related Functional Requirements",
                requirement.RelatedFunctionalRequirements.Count == 0
                    ? "Not specified."
                    : string.Join(", ", requirement.RelatedFunctionalRequirements));
        }
        if (document.Content.NonFunctionalRequirements.Count == 0) text.AppendLine("Not specified.").AppendLine();
        List(text, "6. External Interface Requirements", document.Content.ExternalInterfaceRequirements);
        List(text, "7. Data Requirements", document.Content.DataRequirements);
        List(text, "8. Constraints", document.Content.Constraints);
        List(text, "9. Assumptions and Dependencies", document.Content.AssumptionsAndDependencies);
        List(text, "10. Acceptance Criteria", document.Content.AcceptanceCriteria);
        Section(text, "11. AI Input Quality Summary");
        text.AppendLine(document.Content.AiQualitySummary).AppendLine();
        return text.ToString();
    }

    private static void Section(StringBuilder text, string heading) =>
        text.AppendLine(heading).AppendLine(new string('=', heading.Length));
    private static void Field(StringBuilder text, string label, string value) =>
        text.AppendLine($"{label}: {value}").AppendLine();
    private static void List(StringBuilder text, string heading, IReadOnlyList<string> items)
    {
        Section(text, heading);
        if (items.Count == 0) text.AppendLine("Not specified.");
        else foreach (string item in items) text.AppendLine($"- {item}");
        text.AppendLine();
    }
}
