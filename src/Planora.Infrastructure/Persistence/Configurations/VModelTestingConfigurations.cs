using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;
namespace Planora.Infrastructure.Persistence.Configurations;

public sealed class VModelPhaseConfiguration : IEntityTypeConfiguration<VModelPhase>
{
 public void Configure(EntityTypeBuilder<VModelPhase> b) { b.ToTable("VModelPhases"); b.HasKey(x=>x.Id); b.Property(x=>x.PhaseType).IsRequired(); b.Property(x=>x.PhaseOrder).IsRequired(); b.Property(x=>x.Status).IsRequired(); b.Property(x=>x.CreatedAt).IsRequired(); b.HasIndex(x=>new{x.ProjectId,x.PhaseType}).IsUnique(); b.HasIndex(x=>new{x.ProjectId,x.PhaseOrder}).IsUnique(); b.HasOne(x=>x.Project).WithMany(x=>x.VModelPhases).HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class VModelTestCaseConfiguration : IEntityTypeConfiguration<VModelTestCase>
{
 public void Configure(EntityTypeBuilder<VModelTestCase> b) { b.ToTable("VModelTestCases"); b.HasKey(x=>x.Id); b.Property(x=>x.Identifier).IsRequired().HasMaxLength(50); b.Property(x=>x.Title).IsRequired().HasMaxLength(200); b.Property(x=>x.Description).IsRequired().HasMaxLength(3000); b.Property(x=>x.Preconditions).HasMaxLength(2000); b.Property(x=>x.TestSteps).IsRequired().HasMaxLength(5000); b.Property(x=>x.ExpectedResult).IsRequired().HasMaxLength(3000); b.Property(x=>x.TestLevel).IsRequired(); b.Property(x=>x.CreatedByUserId).IsRequired().HasMaxLength(450); b.Property(x=>x.UpdatedByUserId).HasMaxLength(450); b.Property(x=>x.CreatedAt).IsRequired(); b.HasIndex(x=>new{x.ProjectId,x.Identifier}).IsUnique(); b.HasIndex(x=>new{x.ProjectId,x.TestLevel}); b.HasOne(x=>x.Project).WithMany(x=>x.VModelTestCases).HasForeignKey(x=>x.ProjectId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class VModelTestCaseImplementationArtifactConfiguration : IEntityTypeConfiguration<VModelTestCaseImplementationArtifact>
{
 public void Configure(EntityTypeBuilder<VModelTestCaseImplementationArtifact> b) { b.ToTable("VModelTestCaseImplementationArtifacts"); b.HasKey(x=>new{x.VModelTestCaseId,x.ImplementationArtifactId}); b.HasOne(x=>x.VModelTestCase).WithMany(x=>x.ImplementationArtifacts).HasForeignKey(x=>x.VModelTestCaseId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.ImplementationArtifact).WithMany(x=>x.TestCases).HasForeignKey(x=>x.ImplementationArtifactId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class VModelTestExecutionConfiguration : IEntityTypeConfiguration<VModelTestExecution>
{
 public void Configure(EntityTypeBuilder<VModelTestExecution> b) { b.ToTable("VModelTestExecutions"); b.HasKey(x=>x.Id); b.Property(x=>x.Result).IsRequired(); b.Property(x=>x.ActualResult).IsRequired().HasMaxLength(3000); b.Property(x=>x.Notes).HasMaxLength(3000); b.Property(x=>x.ExecutedByUserId).IsRequired().HasMaxLength(450); b.Property(x=>x.ExecutedAt).IsRequired(); b.HasIndex(x=>new{x.VModelTestCaseId,x.ExecutedAt}); b.HasOne(x=>x.VModelTestCase).WithMany(x=>x.Executions).HasForeignKey(x=>x.VModelTestCaseId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class VModelTestExecutionIssueConfiguration : IEntityTypeConfiguration<VModelTestExecutionIssue>
{
 public void Configure(EntityTypeBuilder<VModelTestExecutionIssue> b) { b.ToTable("VModelTestExecutionIssues"); b.HasKey(x=>new{x.VModelTestExecutionId,x.IssueId}); b.HasOne(x=>x.VModelTestExecution).WithMany(x=>x.Issues).HasForeignKey(x=>x.VModelTestExecutionId).OnDelete(DeleteBehavior.Restrict); b.HasOne(x=>x.Issue).WithMany(x=>x.VModelTestExecutions).HasForeignKey(x=>x.IssueId).OnDelete(DeleteBehavior.Restrict); }
}
public sealed class VModelPhaseValidationConfiguration : IEntityTypeConfiguration<VModelPhaseValidation>
{
 public void Configure(EntityTypeBuilder<VModelPhaseValidation> b) { b.ToTable("VModelPhaseValidations"); b.HasKey(x=>x.Id); b.Property(x=>x.Result).IsRequired(); b.Property(x=>x.Notes).IsRequired().HasMaxLength(3000); b.Property(x=>x.ValidatedByUserId).IsRequired().HasMaxLength(450); b.Property(x=>x.ValidatedAt).IsRequired(); b.HasIndex(x=>new{x.VModelPhaseId,x.ValidatedAt}); b.HasOne(x=>x.VModelPhase).WithMany(x=>x.Validations).HasForeignKey(x=>x.VModelPhaseId).OnDelete(DeleteBehavior.Restrict); }
}
