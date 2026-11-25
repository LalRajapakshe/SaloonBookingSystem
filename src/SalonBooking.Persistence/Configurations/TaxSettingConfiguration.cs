using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class TaxSettingConfiguration : IEntityTypeConfiguration<TaxSetting>
{
    public void Configure(EntityTypeBuilder<TaxSetting> builder)
    {
        builder.HasKey(x => x.TaxSettingId);
        builder.Property(x => x.Name).HasMaxLength(80).IsRequired();
        builder.Property(x => x.RatePercent).HasPrecision(5, 2);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_TaxSetting_Rate",
                "CAST(RatePercent AS REAL) >= 0 AND CAST(RatePercent AS REAL) <= 100");
        });
        builder.HasIndex(x => new { x.TenantId, x.BranchId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
