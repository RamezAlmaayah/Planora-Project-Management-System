namespace Planora.Application.Common.Security;

public static class AuthorizationPolicies
{
    public const string AdminOnly = "AdminOnly";

    public const string CanManageProjects = "CanManageProjects";

    public const string CanManageScrum = "CanManageScrum";

    public const string CanPerformQa = "CanPerformQa";

    public const string CanDevelop = "CanDevelop";

    public const string CanUseAi = "CanUseAi";

    public const string ProjectMember = "ProjectMember";

    public const string ProjectManagement =
        "ProjectManagement";

    public const string ScrumManagement =
        "ScrumManagement";

    public const string ProjectDevelopment =
        "ProjectDevelopment";

    public const string ProjectQa =
        "ProjectQa";
}