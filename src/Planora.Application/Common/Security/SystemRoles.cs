namespace Planora.Application.Common.Security;

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string ProjectManager = "Project Manager";
    public const string ScrumMaster = "Scrum Master";
    public const string Developer = "Developer";
    public const string QaTester = "QA Tester";

    public static readonly string[] All =
    [
        Admin,
        ProjectManager,
        ScrumMaster,
        Developer,
        QaTester
    ];
}