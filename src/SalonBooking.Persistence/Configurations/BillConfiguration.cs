using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class BillConfiguration : IEntityTypeConfiguration<Bill>
{
    public void Configure(EntityTypeBuilder<Bill> builder)
    {
        builder.HasKey(x => x.BillId);
        builder.Property(x => x.BillNumber).HasMaxLength(20);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.VoidReason).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsConcurrencyToken();
        builder.HasIndex(x => new { x.TenantId, x.BillNumber })
            .IsUnique()
            .HasFilter("[BillNumber] IS NOT NULL AND [IsDeleted] = 0");
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.BillDate });
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Bill_Status",
                "[Status] IN ('Draft', 'Finalized', 'Void')");
            table.HasCheckConstraint(
                "CK_Bill_PaymentStatus",
                "[PaymentStatus] IN ('Unpaid', 'PartiallyPaid', 'Paid')");
            table.HasCheckConstraint(
                "CK_Bill_Money",
                "CAST(Subtotal AS REAL) >= 0 AND CAST(TaxAmount AS REAL) >= 0 AND CAST(Total AS REAL) >= 0");
        });
        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Appointment)
            .WithMany()
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
