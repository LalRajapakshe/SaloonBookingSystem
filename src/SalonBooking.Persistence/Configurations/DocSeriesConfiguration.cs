using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Configurations;

public class DocSeriesConfiguration : IEntityTypeConfiguration<DocSeries>
{
    public void Configure(EntityTypeBuilder<DocSeries> builder)
    {
        builder.ToTable("Docs");

        builder.HasKey(x => x.DocId);

        builder.Property(x => x.DocType)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(x => x.Prefix)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.PadLength)
            .IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.DocType })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");

        builder.HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
