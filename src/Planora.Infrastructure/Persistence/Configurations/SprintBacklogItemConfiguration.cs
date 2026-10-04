using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;

namespace Planora.Infrastructure.Persistence.Configurations;

public class SprintBacklogItemConfiguration
    : IEntityTypeConfiguration<SprintBacklogItem>
{
    public void Configure(EntityTypeBuilder<SprintBacklogItem> builder)
    {
        builder.ToTable("SprintBacklogItems");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AddedByUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.AddedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.SprintId,
            x.BacklogItemId
        })
        .IsUnique();

        builder.HasIndex(x => x.BacklogItemId);

        builder.HasOne(x => x.Sprint)
            .WithMany(x => x.BacklogItems)
            .HasForeignKey(x => x.SprintId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.BacklogItem)
            .WithMany(x => x.SprintAssignments)
            .HasForeignKey(x => x.BacklogItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}