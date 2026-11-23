using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class AppointmentServiceStatusHistoryConfiguration
    : IEntityTypeConfiguration<AppointmentServiceStatusHistory>
{
    public void Configure(EntityTypeBuilder<AppointmentServiceStatusHistory> builder)
    {
        builder.HasKey(x => x.AppointmentServiceStatusHistoryId);

        builder.Property(x => x.OldStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.Property(x => x.Action)
            .HasMaxLength(40)
            .IsRequired();

        builder.HasIndex(x => new { x.AppointmentServiceId, x.ChangedAt });
        builder.HasIndex(x => new { x.AppointmentId, x.ChangedAt });

        builder.HasOne(x => x.AppointmentService)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.AppointmentServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Appointment)
            .WithMany()
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
