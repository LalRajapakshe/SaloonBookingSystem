using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class AppointmentServiceScheduleHistoryConfiguration
    : IEntityTypeConfiguration<AppointmentServiceScheduleHistory>
{
    public void Configure(EntityTypeBuilder<AppointmentServiceScheduleHistory> builder)
    {
        builder.HasKey(x => x.AppointmentServiceScheduleHistoryId);

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.AppointmentServiceId, x.ChangedAt });

        builder.HasOne(x => x.AppointmentService)
            .WithMany(x => x.ScheduleHistory)
            .HasForeignKey(x => x.AppointmentServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.OldEmployee)
            .WithMany()
            .HasForeignKey(x => x.OldEmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.NewEmployee)
            .WithMany()
            .HasForeignKey(x => x.NewEmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
