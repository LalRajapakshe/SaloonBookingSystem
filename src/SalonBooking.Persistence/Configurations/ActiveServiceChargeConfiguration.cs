using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class ActiveServiceChargeConfiguration : IEntityTypeConfiguration<ActiveServiceCharge>
{
    public void Configure(EntityTypeBuilder<ActiveServiceCharge> builder)
    {
        builder.ToTable("ActiveServiceCharges");
        builder.HasKey(x => x.AppointmentServiceId);
        builder.HasIndex(x => x.BillLineId).IsUnique();
        builder.HasIndex(x => x.BillId);
        builder.HasOne(x => x.AppointmentService)
            .WithMany()
            .HasForeignKey(x => x.AppointmentServiceId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Bill)
            .WithMany()
            .HasForeignKey(x => x.BillId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.BillLine)
            .WithMany()
            .HasForeignKey(x => x.BillLineId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
