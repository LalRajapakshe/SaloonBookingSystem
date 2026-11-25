using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class RentalReservationConfiguration : IEntityTypeConfiguration<RentalReservation>
{
    public void Configure(EntityTypeBuilder<RentalReservation> builder)
    {
        builder.HasKey(x => x.RentalReservationId);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ChargePerUse).HasPrecision(18, 2);
        builder.Property(x => x.IssueCondition).HasMaxLength(200);
        builder.Property(x => x.ReturnCondition).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.RentalAssetId, x.PlannedIssueAt });
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_RentalReservation_Status", "[Status] IN ('Reserved', 'Issued', 'Returned', 'Cancelled')");
            table.HasCheckConstraint("CK_RentalReservation_Window", "[PlannedReturnAt] > [PlannedIssueAt] AND CAST(ChargePerUse AS REAL) >= 0");
        });
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Asset).WithMany().HasForeignKey(x => x.RentalAssetId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.NoAction);
    }
}
