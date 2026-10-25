using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

/// <summary>
/// One number series for a salon. Branch, category, service, employee, and customer each have their own row.
/// </summary>
public class DocSeries : AuditableEntity
{
    public long DocId { get; set; }

    public long TenantId { get; set; }

    public string DocType { get; set; } = string.Empty;

    public string Prefix { get; set; } = string.Empty;

    public long LastNo { get; set; }

    public int PadLength { get; set; } = 6;

    public Tenant Tenant { get; set; } = null!;
}
