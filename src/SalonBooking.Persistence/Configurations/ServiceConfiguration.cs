using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class ServiceConfiguration
    : IEntityTypeConfiguration<Service>
{
    public void Configure(
        EntityTypeBuilder<Service> builder)
    {
        builder.HasKey(x => x.ServiceId);

        builder.Property(x => x.ServiceCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.ServiceName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(250);

        builder.Property(x => x.DurationMinutes)
            .IsRequired();

        builder.Property(x => x.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Cost)
            .HasPrecision(18, 2);

        builder.Property(x => x.CommissionPercentage)
            .HasPrecision(18, 2);

       builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.ServiceCode
            })
            .IsUnique();

   builder.HasOne(c => c.Tenant)
    .WithMany()
    .HasForeignKey(c => c.TenantId)
    .OnDelete(DeleteBehavior.NoAction);

    builder.HasOne(c => c.Branch)
    .WithMany()
    .HasForeignKey(c => c.BranchId)
    .OnDelete(DeleteBehavior.NoAction);    

   // builder.HasOne(c => c.Tenant)
    
    }
}