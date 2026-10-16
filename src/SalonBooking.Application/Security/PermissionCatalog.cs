namespace SalonBooking.Application.Security;

public sealed record PermissionDefinition(
    string Code,
    string Name,
    string Area,
    string Description);

public static class PermissionCatalog
{
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string RolesRead = "roles.read";
    public const string RolesManage = "roles.manage";
    public const string PermissionsRead = "permissions.read";
    public const string EmployeesRead = "employees.read";
    public const string EmployeesManage = "employees.manage";
    public const string CustomersRead = "customers.read";
    public const string CustomersManage = "customers.manage";
    public const string ServicesRead = "services.read";
    public const string ServicesManage = "services.manage";
    public const string ServiceCategoriesRead = "serviceCategories.read";
    public const string ServiceCategoriesManage = "serviceCategories.manage";
    public const string AppointmentsRead = "appointments.read";
    public const string AppointmentsManage = "appointments.manage";
    public const string AvailabilityRead = "availability.read";
    public const string SchedulesRead = "schedules.read";
    public const string SchedulesManage = "schedules.manage";
    public const string LeavesRead = "leaves.read";
    public const string LeavesManage = "leaves.manage";
    public const string BranchesRead = "branches.read";
    public const string BranchesManage = "branches.manage";
    public const string TenantsRead = "tenants.read";
    public const string TenantsManage = "tenants.manage";
    public const string CustomerProfileRead = "customer.profile.read";
    public const string CustomerAppointmentsRead = "customer.appointments.read";
    public const string CustomerAppointmentsCreate = "customer.appointments.create";

    public const string AdminRoleCode = "ADMIN";
    public const string StaffRoleCode = "STAFF";
    public const string CustomerRoleCode = "CUSTOMER";

    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        Define(UsersRead, "View users", "Users", "View user accounts in this salon."),
        Define(UsersManage, "Manage users", "Users", "Create and update user accounts, including activation."),
        Define(RolesRead, "View roles", "Roles", "View roles and the permissions assigned to them."),
        Define(RolesManage, "Manage roles", "Roles", "Create roles and change their permissions."),
        Define(PermissionsRead, "View permissions", "Permissions", "View the permission catalog."),
        Define(EmployeesRead, "View employees", "Employees", "View employee records."),
        Define(EmployeesManage, "Manage employees", "Employees", "Create and update employee records and service skills."),
        Define(CustomersRead, "View customers", "Customers", "View customer records."),
        Define(CustomersManage, "Manage customers", "Customers", "Create and update customer records and profile photos."),
        Define(ServicesRead, "View services", "Services", "View services and prices."),
        Define(ServicesManage, "Manage services", "Services", "Create and update services."),
        Define(ServiceCategoriesRead, "View service categories", "Services", "View service categories."),
        Define(ServiceCategoriesManage, "Manage service categories", "Services", "Create and update service categories."),
        Define(AppointmentsRead, "View appointments", "Appointments", "View appointments and their services."),
        Define(AppointmentsManage, "Manage appointments", "Appointments", "Create, schedule, and update appointments."),
        Define(AvailabilityRead, "View availability", "Appointments", "View open appointment slots."),
        Define(SchedulesRead, "View staff schedules", "Scheduling", "View staff working hours."),
        Define(SchedulesManage, "Manage staff schedules", "Scheduling", "Create and update staff working hours."),
        Define(LeavesRead, "View staff leave", "Scheduling", "View staff leave."),
        Define(LeavesManage, "Manage staff leave", "Scheduling", "Create and update staff leave."),
        Define(BranchesRead, "View branches", "Branches", "View branches."),
        Define(BranchesManage, "Manage branches", "Branches", "Create and update branches."),
        Define(TenantsRead, "View tenant", "Tenant", "View the salon business profile."),
        Define(TenantsManage, "Manage tenant", "Tenant", "Update the salon business profile. Creating a new business stays closed."),
        Define(CustomerProfileRead, "View own profile", "Customer", "View the signed-in customer's own profile."),
        Define(CustomerAppointmentsRead, "View own appointments", "Customer", "View the signed-in customer's own appointments."),
        Define(CustomerAppointmentsCreate, "Create own appointments", "Customer", "Create an appointment for the signed-in customer.")
    ];

    public static IReadOnlySet<string> StaffCodes { get; } =
        All.Where(item => item.Area != "Customer").Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> OperationalCodes { get; } =
        StaffCodes.Where(code =>
            !code.StartsWith("users.", StringComparison.Ordinal) &&
            !code.StartsWith("roles.", StringComparison.Ordinal) &&
            !code.StartsWith("permissions.", StringComparison.Ordinal) &&
            !string.Equals(code, TenantsManage, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> CustomerCodes { get; } =
        All.Where(item => item.Area == "Customer").Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

    public static bool IsStaffCode(string code) => StaffCodes.Contains(code);

    private static PermissionDefinition Define(string code, string name, string area, string description)
        => new(code, name, area, description);
}
