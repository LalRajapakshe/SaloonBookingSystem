using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class ServiceCategoryConfiguration
    : IEntityTypeConfiguration<ServiceCategory>
{
    public void Configure(
        EntityTypeBuilder<ServiceCategory> builder)
    {
        builder.HasKey(x => x.ServiceCategoryId);

        builder.Property(x => x.CategoryCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.CategoryName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(100);

        builder.Property(x => x.DisplayOrder)
            .IsRequired();

        builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.CategoryName
            })
            .IsUnique();

 //  builder.HasOne(c => c.Tenant)
 //   .WithMany()
 //   .HasForeignKey(c => c.TenantId)
 //   .OnDelete(DeleteBehavior.NoAction);

 //  builder.HasOne(c => c.Branch)
 //   .WithMany()
 //   .HasForeignKey(c => c.BranchId)
//    .OnDelete(DeleteBehavior.NoAction);    

    }
}