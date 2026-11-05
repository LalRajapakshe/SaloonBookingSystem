using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class EmployeeQualificationConfiguration : IEntityTypeConfiguration<EmployeeQualification>
{
    public void Configure(EntityTypeBuilder<EmployeeQualification> builder)
    {
        builder.HasKey(x => x.EmployeeQualificationId);

        builder.Property(x => x.AwardingInstitution)
            .HasMaxLength(200);

        builder.Property(x => x.CertificatePath)
            .HasMaxLength(80);

        builder.Property(x => x.VerificationStatus)
            .HasConversion<int>();

        builder.HasIndex(x => new { x.EmployeeId, x.QualificationId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(x => x.Qualification)
            .WithMany(x => x.EmployeeQualifications)
            .HasForeignKey(x => x.QualificationId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
