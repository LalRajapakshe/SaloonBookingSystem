using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities
{
public class Tenant : AuditableEntity
{
    public long TenantId { get; set; }

    public string TenantCode { get; set; } = string.Empty;

    public string TenantName { get; set; } = string.Empty;

    public string BusinessName { get; set; } = string.Empty;

    public string? BusinessRegistrationNo { get; set; }

    public string? ContactPerson { get; set; }

    public string? PhoneNo { get; set; }

    public string? Email { get; set; }

    public string? Address { get; set; }

    public string? LogoUrl { get; set; }

    public SubscriptionPlan SubscriptionPlan { get; set; }
        = SubscriptionPlan.Trial;

    public DateTime SubscriptionStartDate { get; set; }

    public DateTime SubscriptionEndDate { get; set; }

    public int MaxBranches { get; set; } = 1;

    public int MaxUsers { get; set; } = 5;

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;

    public ICollection<Branch> Branches { get; set; }
        = new List<Branch>();
}
}