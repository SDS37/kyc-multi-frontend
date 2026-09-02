using Kyc.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kyc.Api.Data.Configurations;

public class RegistrationInviteConfiguration : IEntityTypeConfiguration<RegistrationInvite>
{
    public void Configure(EntityTypeBuilder<RegistrationInvite> builder)
    {
        builder.ToTable("registration_invites");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.CodeHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(i => i.CodeHash)
            .IsUnique();

        builder.Property(i => i.CreatedAt).IsRequired();
        builder.Property(i => i.RedeemedAt).IsConcurrencyToken();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(i => i.RedeemedTenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
