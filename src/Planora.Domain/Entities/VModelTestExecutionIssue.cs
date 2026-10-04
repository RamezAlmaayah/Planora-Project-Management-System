namespace Planora.Domain.Entities;
public class VModelTestExecutionIssue
{
    public int VModelTestExecutionId { get; set; } public int IssueId { get; set; }
    public VModelTestExecution VModelTestExecution { get; set; } = null!;
    public Issue Issue { get; set; } = null!;
}
