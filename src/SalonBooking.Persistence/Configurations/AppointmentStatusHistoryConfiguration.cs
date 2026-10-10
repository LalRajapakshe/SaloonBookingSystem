using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class AppointmentStatusHistoryConfiguration : IEntityTypeConfiguration<AppointmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<AppointmentStatusHistory> builder)
    {
        builder.HasKey(x => x.AppointmentStatusHistoryId);

        builder.Property(x => x.OldStatus)
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.NewStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.AppointmentId, x.ChangedAt });

        builder.HasOne(x => x.Appointment)
            .WithMany(x => x.StatusHistory)
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
