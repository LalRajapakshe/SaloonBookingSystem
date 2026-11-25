using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class RetailProductConfiguration : IEntityTypeConfiguration<RetailProduct>
{
    public void Configure(EntityTypeBuilder<RetailProduct> builder)
    {
        builder.HasKey(x => x.RetailProductId);
        builder.Property(x => x.ProductCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ProductName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(250);
        builder.Property(x => x.SellingPrice).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.ProductCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
