using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class QualificationConfiguration : IEntityTypeConfiguration<Qualification>
{
    public void Configure(EntityTypeBuilder<Qualification> builder)
    {
        builder.HasKey(x => x.QualificationId);

        builder.Property(x => x.QualificationName)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(x => x.QualificationLevel)
            .HasMaxLength(80);

        builder.Property(x => x.Description)
            .HasMaxLength(500);

        builder.HasIndex(x => new { x.TenantId, x.QualificationName })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
    }
}
