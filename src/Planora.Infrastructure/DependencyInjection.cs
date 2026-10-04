using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Planora.Application.Abstractions.Common;
using Planora.Application.Abstractions.Ai;
using Planora.Application.Abstractions.Dashboard;
using Planora.Application.Abstractions.Logging;
using Planora.Application.Abstractions.Issues;
using Planora.Application.Abstractions.Identity;
using Planora.Application.Abstractions.Notifications;
using Planora.Application.Abstractions.Projects;
using Planora.Application.Abstractions.Requirements;
using Planora.Application.Abstractions.Security;
using Planora.Application.Abstractions.Srs;
using Planora.Application.Abstractions.Tasks;
using Planora.Application.Abstractions.VModel;
using Planora.Application.Common.Tasks;
using Planora.Infrastructure.Dashboard;
using Planora.Infrastructure.Email;
using Planora.Infrastructure.Gemini;
using Planora.Infrastructure.Identity;
using Planora.Infrastructure.Issues;
using Planora.Infrastructure.Logging;
using Planora.Infrastructure.Notifications;
using Planora.Infrastructure.Persistence;
using Planora.Infrastructure.Projects;
using Planora.Infrastructure.Requirements;
using Planora.Infrastructure.Security;
using Planora.Infrastructure.Services;
using Planora.Infrastructure.Tasks;
using Planora.Infrastructure.VModel;

namespace Planora.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString =
            configuration.GetConnectionString(
                "DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<ApplicationDbContext>(
            options =>
                options.UseSqlServer(
                    connectionString));

        services.AddSingleton<
            IClock,
            SystemClock>();

        services.Configure<EmailOptions>(
            configuration.GetSection(
                EmailOptions.SectionName));

        services.AddScoped<
            IEmailService,
            SmtpEmailService>();

        services.AddScoped<
            IProjectAccessService,
            ProjectAccessService>();

        services.AddScoped<
            ILogReaderService,
            LogReaderService>();

        services.Configure<BootstrapAdminOptions>(
            configuration.GetSection(
                BootstrapAdminOptions.SectionName));

        services.AddScoped<
            IProjectService,
            ProjectService>();

        services.AddScoped<
            IProjectMemberService,
            ProjectMemberService>();

        services.AddScoped<
            IAdminUserService,
            AdminUserService>();

        services.AddScoped<
            ITaskAssignmentService,
            TaskAssignmentService>();

        services.AddScoped<
            ITaskService,
            TaskService>();

        services.Configure<TaskAttachmentOptions>(
            configuration.GetSection(
                TaskAttachmentOptions.SectionName));

        services.Configure<QaEvidenceStorageOptions>(configuration.GetSection(QaEvidenceStorageOptions.SectionName));
        services.AddSingleton<IQaEvidenceStorage, LocalQaEvidenceStorage>();

        services.AddSingleton<
            ITaskAttachmentStorage,
            LocalTaskAttachmentStorage>();

        services.AddScoped<
            ITaskCollaborationService,
            TaskCollaborationService>();

        services.AddScoped<
            IIssueService,
            IssueService>();

        services.AddScoped<
            INotificationService,
            NotificationService>();

        services.AddScoped<
            IRequirementService,
            RequirementService>();

        services.Configure<GeminiOptions>(
            configuration.GetSection(GeminiOptions.SectionName));
        services.AddSingleton<HttpClient>();
        services.AddScoped<IGeminiClient, GeminiClient>();
        services.AddSingleton<IAiInputQualityCache, InMemoryAiInputQualityCache>();
        services.AddScoped<IAiRequirementGenerationService, AiRequirementGenerationService>();
        services.AddSingleton<ISrsDraftCache, InMemorySrsDraftCache>();
        services.AddScoped<ISrsService, SrsService>();

        services.AddScoped<
            IVModelArtifactService,
            VModelArtifactService>();
        services.AddScoped<IVModelTestingService, VModelTestingService>();

        services.AddScoped<
            IProjectAnalyticsService,
            ProjectAnalyticsService>();
        services.AddScoped<
            Planora.Application.Abstractions.Scrum.IBacklogService,
            Planora.Infrastructure.Scrum.BacklogService>();

        services.AddScoped<
    Planora.Application.Abstractions.Scrum.ISprintService,
    Planora.Infrastructure.Scrum.SprintService>();

        return services;
    }
}

