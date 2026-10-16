using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Users_CategoryLinks",
                "([UserCategory] = 1 AND [EmployeeId] IS NULL AND [CustomerId] IS NULL) OR ([UserCategory] = 2 AND [EmployeeId] IS NOT NULL AND [CustomerId] IS NULL) OR ([UserCategory] = 3 AND [EmployeeId] IS NULL AND [CustomerId] IS NOT NULL)");
        });

        builder.Property(x => x.Username)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.UserCategory)
            .HasDefaultValue(UserCategory.General);

        builder.Property(x => x.PasswordHash)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.Username })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Users_Tenant_Username");

        builder.HasIndex(x => x.EmployeeId)
            .IsUnique()
            .HasFilter("[EmployeeId] IS NOT NULL")
            .HasDatabaseName("IX_Users_EmployeeId");

        builder.HasIndex(x => x.CustomerId)
            .IsUnique()
            .HasFilter("[CustomerId] IS NOT NULL")
            .HasDatabaseName("IX_Users_CustomerId");

        builder.HasOne(x => x.Employee)
            .WithOne(x => x.User)
            .HasForeignKey<User>(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Customer)
            .WithMany()
            .HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Branch)
            .WithMany()
            .HasForeignKey(x => x.BranchId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
