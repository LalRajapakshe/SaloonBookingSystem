using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class StaffScheduleConfiguration : IEntityTypeConfiguration<StaffSchedule>
{
    public void Configure(EntityTypeBuilder<StaffSchedule> builder)
    {
        builder.HasKey(x => x.StaffScheduleId);

        builder.Property(x => x.SegmentType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.HasIndex(x => new { x.EmployeeId, x.DayOfWeek, x.SegmentType });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_StaffSchedule_EndAfterStart",
                "[EndTime] > [StartTime]");
            table.HasCheckConstraint(
                "CK_StaffSchedule_DayOfWeek",
                "[DayOfWeek] >= 0 AND [DayOfWeek] <= 6");
        });

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
