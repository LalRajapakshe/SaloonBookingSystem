using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class CustomerConfiguration
    : IEntityTypeConfiguration<Customer>
{
    public void Configure(
        EntityTypeBuilder<Customer> builder)
    {
        builder.HasKey(x => x.CustomerId);

        builder.Property(x => x.CustomerCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.LastName)
            .HasMaxLength(100);

        builder.Property(x => x.MobileNo)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.CustomerCode
            })
            .IsUnique();
    }
}