using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;

namespace Planora.Infrastructure.Persistence.Configurations;

public class RequirementConfiguration
    : IEntityTypeConfiguration<Requirement>
{
    public void Configure(EntityTypeBuilder<Requirement> builder)
    {
        builder.ToTable("Requirements");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Identifier)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(x => x.Type)
            .IsRequired();

        builder.Property(x => x.Priority)
            .IsRequired();

        builder.Property(x => x.Rationale)
            .HasMaxLength(3000);

        builder.Property(x => x.Preconditions)
            .HasMaxLength(2000);

        builder.Property(x => x.ExceptionScenario)
            .HasMaxLength(2000);

        builder.Property(x => x.NfrCategory);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.UpdatedByUserId)
            .HasMaxLength(450);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.ProjectId,
            x.Identifier
        })
        .IsUnique();

        builder.HasIndex(x => new
        {
            x.ProjectId,
            x.Type
        });

        builder.HasIndex(x => new
        {
            x.ProjectId,
            x.Status
        });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.Requirements)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
