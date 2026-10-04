using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;
using Planora.Infrastructure.Identity;

namespace Planora.Infrastructure.Persistence.Configurations;

public class QaReviewConfiguration
    : IEntityTypeConfiguration<QaReview>
{
    public void Configure(EntityTypeBuilder<QaReview> builder)
    {
        builder.ToTable("QaReviews");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.QaUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Result)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(3000);

        builder.Property(x => x.TestedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.TaskItemId,
            x.TestedAt
        });

        builder.HasIndex(x => x.QaUserId);

        builder.HasOne(x => x.TaskItem)
            .WithMany(x => x.QaReviews)
            .HasForeignKey(x => x.TaskItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.QaUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}