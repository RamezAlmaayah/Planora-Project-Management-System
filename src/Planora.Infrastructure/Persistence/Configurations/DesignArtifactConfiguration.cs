using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;

namespace Planora.Infrastructure.Persistence.Configurations;

public sealed class DesignArtifactConfiguration : IEntityTypeConfiguration<DesignArtifact>
{
    public void Configure(EntityTypeBuilder<DesignArtifact> builder)
    {
        builder.ToTable("DesignArtifacts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Identifier).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.Type).IsRequired();
        builder.Property(x => x.CreatedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.HasIndex(x => new { x.ProjectId, x.Identifier }).IsUnique();
        builder.HasIndex(x => new { x.ProjectId, x.Type });
        builder.HasOne(x => x.Project)
            .WithMany(x => x.DesignArtifacts)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
