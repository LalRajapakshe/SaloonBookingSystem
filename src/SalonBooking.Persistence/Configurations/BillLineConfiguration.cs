using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class BillLineConfiguration : IEntityTypeConfiguration<BillLine>
{
    public void Configure(EntityTypeBuilder<BillLine> builder)
    {
        builder.HasKey(x => x.BillLineId);
        builder.Property(x => x.LineType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxRatePercent).HasPrecision(5, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.HasIndex(x => x.AppointmentServiceId);
        builder.HasIndex(x => x.RetailProductId);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_BillLine_Type",
                "[LineType] IN ('Service', 'Product', 'Rental')");
            table.HasCheckConstraint(
                "CK_BillLine_Shape",
                "([LineType] = 'Service' AND [AppointmentServiceId] IS NOT NULL AND [RetailProductId] IS NULL AND [RentalReservationId] IS NULL) OR ([LineType] = 'Product' AND [RetailProductId] IS NOT NULL AND [AppointmentServiceId] IS NULL AND [RentalReservationId] IS NULL) OR ([LineType] = 'Rental' AND [RentalReservationId] IS NOT NULL AND [AppointmentServiceId] IS NULL AND [RetailProductId] IS NULL)");
            table.HasCheckConstraint(
                "CK_BillLine_Money",
                "Quantity > 0 AND CAST(UnitPrice AS REAL) >= 0 AND CAST(DiscountAmount AS REAL) = 0 AND CAST(TaxRatePercent AS REAL) >= 0 AND CAST(TaxRatePercent AS REAL) <= 100 AND CAST(TaxAmount AS REAL) >= 0 AND CAST(LineTotal AS REAL) >= 0");
        });
        builder.HasOne(x => x.Bill)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.BillId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.AppointmentService)
            .WithMany()
            .HasForeignKey(x => x.AppointmentServiceId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.RetailProduct)
            .WithMany()
            .HasForeignKey(x => x.RetailProductId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.RentalReservation)
            .WithMany()
            .HasForeignKey(x => x.RentalReservationId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
