using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SalonBooking.Domain.Entities;

public class TenantConfiguration
    : IEntityTypeConfiguration<Tenant>
{
    public void Configure(
        EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("Tenant");

        builder.HasKey(x => x.TenantId);

        builder.Property(x => x.TenantCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.TenantName)
            .HasMaxLength(200)
            .IsRequired();
    }
}