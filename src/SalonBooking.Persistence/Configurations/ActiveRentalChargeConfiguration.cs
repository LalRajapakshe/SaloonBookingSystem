using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class ActiveRentalChargeConfiguration : IEntityTypeConfiguration<ActiveRentalCharge>
{
    public void Configure(EntityTypeBuilder<ActiveRentalCharge> builder)
    {
        builder.HasKey(x => x.RentalReservationId);
        builder.HasIndex(x => x.BillLineId).IsUnique();
        builder.HasOne(x => x.Reservation).WithMany().HasForeignKey(x => x.RentalReservationId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Bill).WithMany().HasForeignKey(x => x.BillId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.BillLine).WithMany().HasForeignKey(x => x.BillLineId).OnDelete(DeleteBehavior.NoAction);
    }
}
