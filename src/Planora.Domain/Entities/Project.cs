using Planora.Domain.Enums;

namespace Planora.Domain.Entities;

public class Project
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Objectives { get; set; } = string.Empty;

    public string Scope { get; set; } = string.Empty;

    public ProjectMethodology Methodology { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Planning;

    public DateTime StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string CreatedByUserId { get; set; } = string.Empty;

    public string? UpdatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
    public ICollection<ProjectMember> Members { get; set; }
    = new List<ProjectMember>();
    public ICollection<Sprint> Sprints { get; set; }
    = new List<Sprint>();
    public ICollection<BacklogItem> BacklogItems { get; set; }
    = new List<BacklogItem>();

    public ICollection<Issue> Issues { get; set; }
    = new List<Issue>();

    public ICollection<Requirement> Requirements { get; set; }
    = new List<Requirement>();

    public ICollection<DesignArtifact> DesignArtifacts { get; set; }
    = new List<DesignArtifact>();

    public ICollection<ImplementationArtifact> ImplementationArtifacts { get; set; }
    = new List<ImplementationArtifact>();
    public ICollection<VModelPhase> VModelPhases { get; set; } = [];
    public ICollection<VModelTestCase> VModelTestCases { get; set; } = [];
    public ICollection<SrsDocument> SrsDocuments { get; set; } = [];


    public ICollection<Notification> Notifications { get; set; }
    = new List<Notification>();

    public ICollection<ProgressReport> ProgressReports { get; set; }
    = new List<ProgressReport>();
}
