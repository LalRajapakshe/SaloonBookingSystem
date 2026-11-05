using SalonBooking.Domain.Common;

namespace SalonBooking.Domain.Entities;

public class Qualification : TenantEntity
{
    public long QualificationId { get; set; }

    public string QualificationName { get; set; } = string.Empty;

    public string? QualificationLevel { get; set; }

    public string? Description { get; set; }

    public ICollection<EmployeeQualification> EmployeeQualifications { get; set; }
        = new List<EmployeeQualification>();
}
