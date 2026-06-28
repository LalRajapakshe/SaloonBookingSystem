using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.HasKey(b => b.BranchId);

        builder.Property(b => b.BranchCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.HasIndex(b => new { b.TenantId, b.BranchCode })
            .IsUnique();

        builder.Property(b => b.BranchName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(b => b.AddressLine1)
            .HasMaxLength(250);

        builder.Property(b => b.AddressLine2)
            .HasMaxLength(250);

        builder.Property(b => b.City)
            .HasMaxLength(100);

        builder.Property(b => b.PhoneNo)
            .HasMaxLength(20);

        builder.Property(b => b.Email)
            .HasMaxLength(150);

        builder.Property(b => b.ManagerName)
            .HasMaxLength(100);

        builder.Property(b => b.IsHeadOffice)
            .HasDefaultValue(false);

        builder.Property(b => b.IsActive)
            .HasDefaultValue(true);

        builder.Property(b => b.IsDeleted)
            .HasDefaultValue(false);

        builder.HasOne(b => b.Tenant)
            .WithMany(t => t.Branches)
            .HasForeignKey(b => b.TenantId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}