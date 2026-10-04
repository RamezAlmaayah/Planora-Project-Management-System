using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;

namespace Planora.Infrastructure.Persistence.Configurations;

public sealed class DesignArtifactRequirementConfiguration
    : IEntityTypeConfiguration<DesignArtifactRequirement>
{
    public void Configure(EntityTypeBuilder<DesignArtifactRequirement> builder)
    {
        builder.ToTable("DesignArtifactRequirements");
        builder.HasKey(x => new { x.DesignArtifactId, x.RequirementId });
        builder.HasOne(x => x.DesignArtifact)
            .WithMany(x => x.Requirements)
            .HasForeignKey(x => x.DesignArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Requirement)
            .WithMany(x => x.DesignArtifacts)
            .HasForeignKey(x => x.RequirementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
