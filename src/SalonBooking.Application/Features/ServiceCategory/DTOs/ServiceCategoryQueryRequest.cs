namespace SalonBooking.Application.Features.ServiceCategory.DTOs;

public class ServiceCategoryQueryRequest
{
    public string? Search { get; set; }

   // public string? Gender { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string SortBy { get; set; } = "ServiceCategoryId";

    public string SortOrder { get; set; } = "asc";
}


