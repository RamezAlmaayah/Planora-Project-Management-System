using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Planora.Domain.Entities;

namespace Planora.Infrastructure.Persistence.Configurations;

public sealed class SrsDocumentConfiguration : IEntityTypeConfiguration<SrsDocument>
{
    public void Configure(EntityTypeBuilder<SrsDocument> builder)
    {
        builder.ToTable("SrsDocuments");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Title).IsRequired().HasMaxLength(200);
        builder.Property(document => document.StructuredContentJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(document => document.QualityScore).IsRequired();
        builder.Property(document => document.QualityLevel).IsRequired().HasMaxLength(32);
        builder.Property(document => document.GeneratedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(document => document.GeneratedAt).IsRequired();
        builder.Property(document => document.SavedByUserId).IsRequired().HasMaxLength(450);
        builder.Property(document => document.SavedAt).IsRequired();
        builder.HasIndex(document => new { document.ProjectId, document.SavedAt });
        builder.HasOne(document => document.Project)
            .WithMany(project => project.SrsDocuments)
            .HasForeignKey(document => document.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
