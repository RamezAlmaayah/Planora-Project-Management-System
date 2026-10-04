using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;

namespace Planora.Infrastructure.Persistence.Configurations;

public class ProgressReportConfiguration
    : IEntityTypeConfiguration<ProgressReport>
{
    public void Configure(EntityTypeBuilder<ProgressReport> builder)
    {
        builder.ToTable("ProgressReports");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ProgressPercentage)
            .HasPrecision(5, 2)
            .IsRequired();

        builder.Property(x => x.Summary)
            .IsRequired()
            .HasMaxLength(3000);

        builder.Property(x => x.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.ProjectId,
            x.CreatedAt
        });

        builder.HasOne(x => x.Project)
            .WithMany(x => x.ProgressReports)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}