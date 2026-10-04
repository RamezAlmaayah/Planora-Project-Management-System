using Planora.Domain.Enums;
namespace Planora.Domain.Entities;
public class VModelTestExecution
{
    public int Id { get; set; } public int VModelTestCaseId { get; set; }
    public VModelTestResult Result { get; set; } public string ActualResult { get; set; } = string.Empty;
    public string? Notes { get; set; } public string ExecutedByUserId { get; set; } = string.Empty;
    public DateTime ExecutedAt { get; set; } public VModelTestCase VModelTestCase { get; set; } = null!;
    public ICollection<VModelTestExecutionIssue> Issues { get; set; } = [];
}
