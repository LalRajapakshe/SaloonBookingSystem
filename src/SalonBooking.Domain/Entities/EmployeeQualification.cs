using SalonBooking.Domain.Common;
using SalonBooking.Domain.Enums;

namespace SalonBooking.Domain.Entities;

public class EmployeeQualification : TenantEntity
{
    public long EmployeeQualificationId { get; set; }

    public long EmployeeId { get; set; }

    public long QualificationId { get; set; }

    public string? AwardingInstitution { get; set; }

    public int? CompletionYear { get; set; }

    public string? CertificatePath { get; set; }

    public QualificationVerificationStatus VerificationStatus { get; set; }
        = QualificationVerificationStatus.Pending;

    public bool IsCustomerVisible { get; set; }

    public Employee Employee { get; set; } = null!;

    public Qualification Qualification { get; set; } = null!;
}
