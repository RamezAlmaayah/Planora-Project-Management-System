using Planora.Domain.Enums;
namespace Planora.Domain.Entities;
public class VModelTestCase
{
    public int Id { get; set; } public int ProjectId { get; set; }
    public string Identifier { get; set; } = string.Empty; public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty; public VModelTestLevel TestLevel { get; set; }
    public string? Preconditions { get; set; } public string TestSteps { get; set; } = string.Empty;
    public string ExpectedResult { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty; public DateTime CreatedAt { get; set; }
    public string? UpdatedByUserId { get; set; } public DateTime? UpdatedAt { get; set; }
    public Project Project { get; set; } = null!;
    public ICollection<VModelTestCaseImplementationArtifact> ImplementationArtifacts { get; set; } = [];
    public ICollection<VModelTestExecution> Executions { get; set; } = [];
}
