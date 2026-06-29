namespace SalonBooking.Application.Features.Tenant.DTOs;

public class TenantQueryRequest
{
    public string? Search { get; set; }

    public bool? IsActive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string SortBy { get; set; } = "TenantId";

    public string SortOrder { get; set; } = "asc";
}