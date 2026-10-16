using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class EmployeeConfiguration
    : IEntityTypeConfiguration<Employee>
{
    public void Configure(
        EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(x => x.EmployeeId);

        builder.Property(x => x.EmployeeCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.LastName)
            .HasMaxLength(100);

        builder.Property(x => x.MobileNo)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Email)
            .HasMaxLength(200);

        builder.Property(x => x.Gender)
            .HasMaxLength(20);

        builder.Property(x => x.Address)
            .HasMaxLength(250);

        builder.Property(x => x.Designation)
            .HasMaxLength(100);
            
        builder.Property(x => x.Salary)
       .HasPrecision(18, 2);

        builder.HasIndex(x =>
            new
            {
                x.TenantId,
                x.EmployeeCode
            })
            .IsUnique();

   builder.HasOne(c => c.Tenant)
    .WithMany()
    .HasForeignKey(c => c.TenantId)
    .OnDelete(DeleteBehavior.NoAction);

    builder.HasOne(c => c.Branch)
    .WithMany()
    .HasForeignKey(c => c.BranchId)
    .OnDelete(DeleteBehavior.NoAction);    

   // builder.HasOne(c => c.Tenant)
    
    }
}