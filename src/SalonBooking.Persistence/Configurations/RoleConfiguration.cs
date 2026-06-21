using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class RoleConfiguration :
    IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("Role");

        builder.HasKey(x => x.RoleId);

        builder.Property(x => x.RoleCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.RoleName)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(x =>
            new { x.TenantId, x.RoleCode })
            .IsUnique();
    }
}