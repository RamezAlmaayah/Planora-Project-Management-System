using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;

namespace Planora.Infrastructure.Persistence.Configurations;

public class ActivityLogConfiguration
    : IEntityTypeConfiguration<ActivityLog>
{
    public void Configure(EntityTypeBuilder<ActivityLog> builder)
    {
        builder.ToTable("ActivityLogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorUserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ResourceType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ResourceId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Description)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(x => x.OldValues)
            .HasMaxLength(4000);

        builder.Property(x => x.NewValues)
            .HasMaxLength(4000);

        builder.Property(x => x.TraceId)
            .HasMaxLength(100);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.HasIndex(x => new
        {
            x.ActorUserId,
            x.CreatedAt
        });

        builder.HasIndex(x => new
        {
            x.ProjectId,
            x.CreatedAt
        });

        builder.HasIndex(x => new
        {
            x.Action,
            x.CreatedAt
        });

        builder.HasIndex(x => x.CreatedAt);
    }
}