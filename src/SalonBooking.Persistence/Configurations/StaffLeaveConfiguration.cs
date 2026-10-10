using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class StaffLeaveConfiguration : IEntityTypeConfiguration<StaffLeave>
{
    public void Configure(EntityTypeBuilder<StaffLeave> builder)
    {
        builder.HasKey(x => x.StaffLeaveId);

        builder.Property(x => x.LeaveType)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.EmployeeId, x.StartDateTime, x.EndDateTime, x.Status });

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_StaffLeave_EndAfterStart",
                "[EndDateTime] > [StartDateTime]");
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
