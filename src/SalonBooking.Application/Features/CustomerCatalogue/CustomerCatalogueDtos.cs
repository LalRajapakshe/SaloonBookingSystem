namespace SalonBooking.Application.Features.CustomerCatalogue;

public sealed class CustomerBranchResponse
{
    public long BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public string? Highlight { get; set; }

    public string? AddressLine1 { get; set; }

    public string? AddressLine2 { get; set; }

    public string? City { get; set; }

    public string? PhoneNo { get; set; }

    public string? Email { get; set; }

    public bool HasPhoto { get; set; }
}

public sealed class CustomerServiceResponse
{
    public long ServiceId { get; set; }

    public long BranchId { get; set; }

    public long ServiceCategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public string ServiceName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DurationMinutes { get; set; }

    public decimal Price { get; set; }

    public bool IsPopular { get; set; }

    public bool HasPhoto { get; set; }
}

public sealed class CustomerStylistResponse
{
    public long EmployeeId { get; set; }

    public long BranchId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? Specialisation { get; set; }

    public string? ProfessionalDescription { get; set; }

    public bool HasPhoto { get; set; }

    public List<CustomerQualificationResponse> Qualifications { get; set; } = [];
}

public sealed class CustomerQualificationResponse
{
    public long EmployeeQualificationId { get; set; }

    public string QualificationName { get; set; } = string.Empty;

    public string? QualificationLevel { get; set; }

    public string? AwardingInstitution { get; set; }

    public int? CompletionYear { get; set; }

    public string VerificationStatus { get; set; } = string.Empty;

    public bool IsVerified { get; set; }
}

public sealed class CustomerAvailabilityResponse
{
    public long BranchId { get; set; }

    public long ServiceId { get; set; }

    public long? EmployeeId { get; set; }

    public int DurationMinutes { get; set; }

    public List<CustomerSlotResponse> Slots { get; set; } = [];
}

public sealed class CustomerSlotResponse
{
    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public bool Available { get; set; }
}
