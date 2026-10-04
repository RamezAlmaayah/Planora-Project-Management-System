using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;
using Planora.Infrastructure.Identity;

namespace Planora.Infrastructure.Persistence.Configurations;

public class QaEvidenceConfiguration
    : IEntityTypeConfiguration<QaEvidence>
{
    public void Configure(EntityTypeBuilder<QaEvidence> builder)
    {
        builder.ToTable("QaEvidenceFiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.StorageKey)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.FileSizeBytes)
            .IsRequired();

        builder.Property(x => x.Extension)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.UploadedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.UploadedAt)
            .IsRequired();

        builder.HasIndex(x => x.QaReviewId);

        builder.HasIndex(x => x.StorageKey)
            .IsUnique();

        builder.HasOne(x => x.QaReview)
            .WithMany(x => x.EvidenceFiles)
            .HasForeignKey(x => x.QaReviewId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}