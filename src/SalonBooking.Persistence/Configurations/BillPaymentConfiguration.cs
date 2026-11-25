using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class BillPaymentConfiguration : IEntityTypeConfiguration<BillPayment>
{
    public void Configure(EntityTypeBuilder<BillPayment> builder)
    {
        builder.HasKey(x => x.BillPaymentId);
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => x.BillId);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_BillPayment_Shape",
                "(Kind = 'Payment' AND CAST(Amount AS REAL) > 0) OR (Kind = 'Refund' AND CAST(Amount AS REAL) < 0)");
            table.HasCheckConstraint(
                "CK_BillPayment_Method",
                "[Method] IN ('Cash', 'Card', 'BankTransfer', 'Other')");
        });
        builder.HasOne(x => x.Bill)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.BillId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
