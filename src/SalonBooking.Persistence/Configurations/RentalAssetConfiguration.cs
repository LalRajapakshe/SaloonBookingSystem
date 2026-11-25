using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class RentalAssetConfiguration : IEntityTypeConfiguration<RentalAsset>
{
    public void Configure(EntityTypeBuilder<RentalAsset> builder)
    {
        builder.HasKey(x => x.RentalAssetId);
        builder.Property(x => x.AssetCode).HasMaxLength(40).IsRequired();
        builder.Property(x => x.AssetName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(250);
        builder.Property(x => x.Category).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ChargePerUse).HasPrecision(18, 2);
        builder.Property(x => x.ConditionNotes).HasMaxLength(500);
        builder.Property(x => x.PhotoPath).HasMaxLength(260);
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.AssetCode })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_RentalAsset_Money", "CAST(ChargePerUse AS REAL) >= 0 AND CleaningBufferMinutes >= 0 AND CleaningBufferMinutes <= 10080");
        });
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.NoAction);
    }
}
