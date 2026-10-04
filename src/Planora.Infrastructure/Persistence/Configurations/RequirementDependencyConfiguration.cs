using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;

namespace Planora.Infrastructure.Persistence.Configurations;

public class RequirementDependencyConfiguration
    : IEntityTypeConfiguration<RequirementDependency>
{
    public void Configure(EntityTypeBuilder<RequirementDependency> builder)
    {
        builder.ToTable("RequirementDependencies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.RequirementId,
            x.DependsOnRequirementId
        })
        .IsUnique();

        builder.HasOne(x => x.Requirement)
            .WithMany(x => x.Dependencies)
            .HasForeignKey(x => x.RequirementId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.DependsOnRequirement)
            .WithMany(x => x.Dependents)
            .HasForeignKey(x => x.DependsOnRequirementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}