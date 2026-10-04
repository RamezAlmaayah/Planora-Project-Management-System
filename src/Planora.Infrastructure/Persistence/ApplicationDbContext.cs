using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Planora.Domain.Entities;
using Planora.Infrastructure.Identity;

namespace Planora.Infrastructure.Persistence;

public class ApplicationDbContext
    : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<Sprint> Sprints => Set<Sprint>();

    public DbSet<BacklogItem> BacklogItems => Set<BacklogItem>();

    public DbSet<SprintBacklogItem> SprintBacklogItems =>
        Set<SprintBacklogItem>();

    public DbSet<TaskItem> TaskItems => Set<TaskItem>();

    public DbSet<QaReview> QaReviews => Set<QaReview>();

    public DbSet<QaEvidence> QaEvidenceFiles => Set<QaEvidence>();

    public DbSet<TaskAttachment> TaskAttachments =>
        Set<TaskAttachment>();

    public DbSet<TaskComment> TaskComments => Set<TaskComment>();

    public DbSet<Issue> Issues => Set<Issue>();

    public DbSet<Requirement> Requirements => Set<Requirement>();

    public DbSet<RequirementDependency> RequirementDependencies =>
        Set<RequirementDependency>();

    public DbSet<RequirementTrace> RequirementTraces =>
        Set<RequirementTrace>();

    public DbSet<DesignArtifact> DesignArtifacts => Set<DesignArtifact>();

    public DbSet<DesignArtifactRequirement> DesignArtifactRequirements =>
        Set<DesignArtifactRequirement>();

    public DbSet<ImplementationArtifact> ImplementationArtifacts =>
        Set<ImplementationArtifact>();

    public DbSet<ImplementationArtifactDesign> ImplementationArtifactDesigns =>
        Set<ImplementationArtifactDesign>();
    public DbSet<VModelPhase> VModelPhases => Set<VModelPhase>();
    public DbSet<VModelTestCase> VModelTestCases => Set<VModelTestCase>();
    public DbSet<VModelTestCaseImplementationArtifact> VModelTestCaseImplementationArtifacts => Set<VModelTestCaseImplementationArtifact>();
    public DbSet<VModelTestExecution> VModelTestExecutions => Set<VModelTestExecution>();
    public DbSet<VModelTestExecutionIssue> VModelTestExecutionIssues => Set<VModelTestExecutionIssue>();
    public DbSet<VModelPhaseValidation> VModelPhaseValidations => Set<VModelPhaseValidation>();
    public DbSet<SrsDocument> SrsDocuments => Set<SrsDocument>();

    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    public DbSet<ProgressReport> ProgressReports =>
        Set<ProgressReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
    }
}
