using Kyc.Api.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kyc.Api.Data.Configurations;

public class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(d => d.ContentType)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(d => d.StorageKey)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(d => d.SizeBytes).IsRequired();
        builder.Property(d => d.UploadedAt).IsRequired();

        builder.HasOne(d => d.Tenant)
            .WithMany()
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(d => d.Case)
            .WithMany()
            .HasForeignKey(d => d.CaseId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasOne(d => d.UploadedByUser)
            .WithMany()
            .HasForeignKey(d => d.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(d => new { d.TenantId, d.CaseId });
        builder.HasIndex(d => d.StorageKey).IsUnique();
    }
}
