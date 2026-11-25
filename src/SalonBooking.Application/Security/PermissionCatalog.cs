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
    public const string AppointmentsOwnRead = "appointments.own.read";
    public const string AppointmentsOwnProgress = "appointments.own.progress";
    public const string AppointmentsLinesProgress = "appointments.lines.progress";
    public const string NotificationsBookingsRead = "notifications.bookings.read";
    public const string NotificationsCancellationsRead = "notifications.cancellations.read";
    public const string NotificationsConfirmationRead = "notifications.confirmation.read";
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
    public const string CustomerCatalogueRead = "customer.catalogue.read";
    public const string CustomerAvailabilityRead = "customer.availability.read";
    public const string QualificationsRead = "qualifications.read";
    public const string QualificationsManage = "qualifications.manage";
    public const string ProductsRead = "products.read";
    public const string ProductsManage = "products.manage";
    public const string BillsRead = "bills.read";
    public const string BillsManage = "bills.manage";
    public const string PaymentsCollect = "payments.collect";
    public const string TaxRead = "tax.read";
    public const string TaxManage = "tax.manage";
    public const string RentalsRead = "rentals.read";
    public const string RentalsManage = "rentals.manage";
    public const string RentalsIssue = "rentals.issue";
    public const string PlatformManage = "platform.manage";

    public const string AdminRoleCode = "ADMIN";
    public const string StaffRoleCode = "STAFF";
    public const string CustomerRoleCode = "CUSTOMER";
    public const string StylistRoleCode = "STYLIST";
    public const string ReceptionistRoleCode = "RECEPTIONIST";
    public const string SupervisorRoleCode = "SUPERVISOR";

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
        Define(AppointmentsOwnRead, "View own appointments", "Appointments", "View appointments and service lines assigned to the signed-in employee."),
        Define(AppointmentsOwnProgress, "Progress own services", "Appointments", "Commence and complete service lines assigned to the signed-in employee."),
        Define(AppointmentsLinesProgress, "Progress branch services", "Appointments", "Commence and complete service lines assigned to other employees in an authorized branch."),
        Define(NotificationsBookingsRead, "View new bookings", "Notifications", "View new booking notifications."),
        Define(NotificationsCancellationsRead, "View cancellation requests", "Notifications", "View customer cancellation requests."),
        Define(NotificationsConfirmationRead, "View confirmations", "Notifications", "View appointments awaiting confirmation."),
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
        Define(CustomerAppointmentsCreate, "Create own appointments", "Customer", "Create an appointment for the signed-in customer."),
        Define(CustomerCatalogueRead, "View salon catalogue", "Customer", "View published branches, services, and stylists in this salon."),
        Define(CustomerAvailabilityRead, "View appointment times", "Customer", "View open and closed appointment times."),
        Define(QualificationsRead, "View qualifications", "Qualifications", "View qualification records and stylist qualifications."),
        Define(QualificationsManage, "Manage qualifications", "Qualifications", "Create qualifications and maintain stylist qualifications."),
        Define(ProductsRead, "View retail products", "Retail", "View the retail product catalogue."),
        Define(ProductsManage, "Manage retail products", "Retail", "Create and update retail products. This does not track stock."),
        Define(BillsRead, "View bills", "Billing", "View bills, receipts, and service billing coverage."),
        Define(BillsManage, "Manage bills", "Billing", "Create, edit, finalize, and void bills."),
        Define(PaymentsCollect, "Collect payments", "Billing", "Record payments and refunds on finalized bills."),
        Define(TaxRead, "View tax", "Billing", "View the branch tax rate."),
        Define(TaxManage, "Manage tax", "Billing", "Set the branch tax rate. No rate is assumed."),
        Define(RentalsRead, "View rentals", "Rentals", "View rental assets and reservations."),
        Define(RentalsManage, "Manage rentals", "Rentals", "Create rental assets and reservations."),
        Define(RentalsIssue, "Issue and return rentals", "Rentals", "Issue and receive rental assets."),
        Define(PlatformManage, "Manage the platform", "Platform", "Create salon businesses and branches. This permission cannot be granted by a role.")
    ];

    public static IReadOnlySet<string> StaffCodes { get; } =
        All.Where(item => item.Area is not "Customer" and not "Platform").Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// These codes are assigned by the role matrix below.
    /// Existing role rows are not backfilled. The generic STAFF role does not receive them.
    /// A newly created ADMIN role still receives every staff code except own-appointment progress.
    /// </summary>
    public static IReadOnlySet<string> ExplicitAssignmentCodes { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AppointmentsOwnRead,
            AppointmentsOwnProgress,
            AppointmentsLinesProgress,
            NotificationsBookingsRead,
            NotificationsCancellationsRead,
            NotificationsConfirmationRead,
            ProductsRead,
            ProductsManage,
            BillsRead,
            BillsManage,
            PaymentsCollect,
            TaxRead,
            TaxManage,
            RentalsRead,
            RentalsManage,
            RentalsIssue
        };

    public static IReadOnlySet<string> NotificationReadCodes { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            NotificationsBookingsRead,
            NotificationsCancellationsRead,
            NotificationsConfirmationRead
        };

    /// <summary>
    /// Salon admin keeps management access and the notification queue.
    /// Own-appointment and service-line progress codes stay on the stylist and supervisor roles.
    /// </summary>
    public static IReadOnlySet<string> AdminRoleCodes { get; } =
        StaffCodes
            .Where(code =>
                !string.Equals(code, AppointmentsOwnRead, StringComparison.Ordinal) &&
                !string.Equals(code, AppointmentsOwnProgress, StringComparison.Ordinal) &&
                !string.Equals(code, AppointmentsLinesProgress, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> OperationalCodes { get; } =
        StaffCodes.Where(code =>
            !code.StartsWith("users.", StringComparison.Ordinal) &&
            !code.StartsWith("roles.", StringComparison.Ordinal) &&
            !code.StartsWith("permissions.", StringComparison.Ordinal) &&
            !code.StartsWith("qualifications.", StringComparison.Ordinal) &&
            !string.Equals(code, TenantsManage, StringComparison.Ordinal) &&
            !ExplicitAssignmentCodes.Contains(code))
            .ToHashSet(StringComparer.Ordinal);

    public static IReadOnlySet<string> StylistRoleCodes { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AppointmentsOwnRead
        };

    public static IReadOnlySet<string> ReceptionistRoleCodes { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AppointmentsRead,
            AppointmentsManage,
            CustomersRead,
            CustomersManage,
            ServicesRead,
            BranchesRead,
            SchedulesRead,
            AvailabilityRead,
            NotificationsBookingsRead,
            NotificationsCancellationsRead,
            NotificationsConfirmationRead,
            AppointmentsLinesProgress,
            ProductsRead,
            BillsRead,
            BillsManage,
            PaymentsCollect,
            TaxRead,
            RentalsRead,
            RentalsManage,
            RentalsIssue
        };

    public static IReadOnlySet<string> SupervisorRoleCodes { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AppointmentsRead,
            SchedulesRead,
            AppointmentsLinesProgress,
            BillsRead
        };

    /// <summary>
    /// A salon admin does not hold these codes, but may still assign the stylist and supervisor roles.
    /// </summary>
    public static bool CanDelegateWithoutHolding(string code) =>
        string.Equals(code, AppointmentsOwnRead, StringComparison.Ordinal) ||
        string.Equals(code, AppointmentsOwnProgress, StringComparison.Ordinal) ||
        string.Equals(code, AppointmentsLinesProgress, StringComparison.Ordinal);

    public static IReadOnlySet<string> CustomerCodes { get; } =
        All.Where(item => item.Area == "Customer").Select(item => item.Code).ToHashSet(StringComparer.Ordinal);

    public static bool IsStaffCode(string code) => StaffCodes.Contains(code);

    private static PermissionDefinition Define(string code, string name, string area, string description)
        => new(code, name, area, description);
}
