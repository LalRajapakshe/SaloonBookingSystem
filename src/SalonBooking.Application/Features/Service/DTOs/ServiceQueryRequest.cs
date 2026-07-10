namespace SalonBooking.Application.Features.Service.DTOs
{
public class ServiceQueryRequest
{
    public string? Search { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string SortBy { get; set; } = "ServiceId";

    public string SortOrder { get; set; } = "asc";
}
}