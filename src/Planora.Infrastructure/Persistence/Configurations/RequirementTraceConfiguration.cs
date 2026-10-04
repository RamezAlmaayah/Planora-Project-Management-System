using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;

namespace Planora.Infrastructure.Persistence.Configurations;

public class RequirementTraceConfiguration
    : IEntityTypeConfiguration<RequirementTrace>
{
    public void Configure(EntityTypeBuilder<RequirementTrace> builder)
    {
        builder.ToTable("RequirementTraces");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Stage)
            .IsRequired();

        builder.Property(x => x.ReferenceCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.RequirementId,
            x.Stage,
            x.ReferenceCode
        })
        .IsUnique();

        builder.HasOne(x => x.Requirement)
            .WithMany(x => x.Traces)
            .HasForeignKey(x => x.RequirementId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}