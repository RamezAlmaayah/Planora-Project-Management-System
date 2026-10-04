using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;

namespace Planora.Infrastructure.Persistence.Configurations;

public sealed class ImplementationArtifactDesignConfiguration
    : IEntityTypeConfiguration<ImplementationArtifactDesign>
{
    public void Configure(EntityTypeBuilder<ImplementationArtifactDesign> builder)
    {
        builder.ToTable("ImplementationArtifactDesigns");
        builder.HasKey(x => new { x.ImplementationArtifactId, x.DesignArtifactId });
        builder.HasOne(x => x.ImplementationArtifact)
            .WithMany(x => x.Designs)
            .HasForeignKey(x => x.ImplementationArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.DesignArtifact)
            .WithMany(x => x.ImplementationArtifacts)
            .HasForeignKey(x => x.DesignArtifactId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
