namespace SalonBooking.Application.Features.Branch.DTOs
{
public class BranchQueryRequest
{
    public string? Search { get; set; }

    public long TenantId { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;

    public string SortBy { get; set; } = "BranchId";

    public string SortOrder { get; set; } = "asc";
}
}