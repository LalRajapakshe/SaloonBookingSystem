using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.HasKey(x => x.AppointmentId);

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.CancellationReason)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.BranchId, x.AppointmentDate, x.Status });

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.CancelledByUser)
            .WithMany()
            .HasForeignKey(x => x.CancelledByUserId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
