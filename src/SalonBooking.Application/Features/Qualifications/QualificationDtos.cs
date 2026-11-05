namespace SalonBooking.Application.Features.Qualifications;

public sealed class QualificationResponse
{
    public long QualificationId { get; set; }

    public string QualificationName { get; set; } = string.Empty;

    public string? QualificationLevel { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; }
}

public sealed class SaveQualificationRequest
{
    public string QualificationName { get; set; } = string.Empty;

    public string? QualificationLevel { get; set; }

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class EmployeeQualificationResponse
{
    public long EmployeeQualificationId { get; set; }

    public long EmployeeId { get; set; }

    public long QualificationId { get; set; }

    public string QualificationName { get; set; } = string.Empty;

    public string? QualificationLevel { get; set; }

    public string? AwardingInstitution { get; set; }

    public int? CompletionYear { get; set; }

    public string VerificationStatus { get; set; } = string.Empty;

    public bool IsCustomerVisible { get; set; }

    public bool HasCertificate { get; set; }
}

public sealed class SaveEmployeeQualificationRequest
{
    public long EmployeeId { get; set; }

    public long QualificationId { get; set; }

    public string? AwardingInstitution { get; set; }

    public int? CompletionYear { get; set; }

    public string VerificationStatus { get; set; } = "Pending";

    public bool IsCustomerVisible { get; set; }
}
