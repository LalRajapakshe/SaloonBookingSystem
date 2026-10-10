using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class EmployeeServiceConfiguration : IEntityTypeConfiguration<EmployeeService>
{
    public void Configure(EntityTypeBuilder<EmployeeService> builder)
    {
        builder.HasKey(x => x.EmployeeServiceId);

        builder.HasIndex(x => new { x.EmployeeId, x.ServiceId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasIndex(x => new { x.BranchId, x.ServiceId, x.IsActive });

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Service)
            .WithMany()
            .HasForeignKey(x => x.ServiceId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
