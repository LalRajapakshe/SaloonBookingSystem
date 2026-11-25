using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class ServiceFeedbackConfiguration : IEntityTypeConfiguration<ServiceFeedback>
{
    public void Configure(EntityTypeBuilder<ServiceFeedback> builder)
    {
        builder.HasKey(x => x.ServiceFeedbackId);
        builder.Property(x => x.Comment).HasMaxLength(1000);
        builder.HasIndex(x => x.AppointmentServiceId).IsUnique().HasFilter("[IsDeleted] = 0");
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ServiceFeedback_Rating", "[Rating] >= 1 AND [Rating] <= 5");
        });
        builder.HasOne(x => x.AppointmentService).WithMany().HasForeignKey(x => x.AppointmentServiceId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.NoAction);
    }
}
