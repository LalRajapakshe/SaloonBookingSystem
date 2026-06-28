using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.HasKey(t => t.TenantId);

        builder.Property(t => t.TenantCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(t => t.TenantCode)
            .IsUnique();

        builder.Property(t => t.TenantName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(t => t.BusinessName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.BusinessRegistrationNo)
            .HasMaxLength(100);

        builder.Property(t => t.ContactPerson)
            .HasMaxLength(100);

        builder.Property(t => t.PhoneNo)
            .HasMaxLength(20);

        builder.Property(t => t.Email)
            .HasMaxLength(150);

        builder.Property(t => t.Address)
            .HasMaxLength(500);

        builder.Property(t => t.LogoUrl)
            .HasMaxLength(500);

        builder.Property(t => t.MaxBranches)
            .HasDefaultValue(1);

        builder.Property(t => t.MaxUsers)
            .HasDefaultValue(5);

        builder.Property(t => t.IsActive)
            .HasDefaultValue(true);

        builder.Property(t => t.IsDeleted)
            .HasDefaultValue(false);

        builder.HasMany(t => t.Branches)
            .WithOne(b => b.Tenant)
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}