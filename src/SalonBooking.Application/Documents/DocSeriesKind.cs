namespace SalonBooking.Application.Documents;

public static class DocSeriesKind
{
    public const string Branch = "Branch";
    public const string Category = "Category";
    public const string Service = "Service";
    public const string Employee = "Employee";
    public const string Customer = "Customer";

    public static readonly IReadOnlyList<(string Kind, string Prefix)> All =
    [
        (Branch, "BRN"),
        (Category, "CAT"),
        (Service, "SER"),
        (Employee, "EMP"),
        (Customer, "CUS")
    ];
}
