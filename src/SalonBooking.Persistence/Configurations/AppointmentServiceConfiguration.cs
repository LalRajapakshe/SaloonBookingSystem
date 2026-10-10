using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class AppointmentServiceConfiguration : IEntityTypeConfiguration<AppointmentService>
{
    public void Configure(EntityTypeBuilder<AppointmentService> builder)
    {
        builder.HasKey(x => x.AppointmentServiceId);

        builder.Property(x => x.UnitPrice)
            .HasPrecision(18, 2);

        builder.Property(x => x.Notes)
            .HasMaxLength(1000);

        builder.Property(x => x.RowVersion)
            .IsConcurrencyToken();

        builder.HasIndex(x => new { x.AppointmentId, x.Sequence })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => new { x.EmployeeId, x.StartTime, x.EndTime });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_AppointmentService_ScheduleShape",
                "([StartTime] IS NULL AND [EndTime] IS NULL) OR ([EmployeeId] IS NOT NULL AND [StartTime] IS NOT NULL AND [EndTime] IS NOT NULL AND [EndTime] > [StartTime])");
        });

        builder.HasOne(x => x.Appointment)
            .WithMany(x => x.AppointmentServices)
            .HasForeignKey(x => x.AppointmentId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
