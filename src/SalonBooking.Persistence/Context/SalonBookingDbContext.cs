

using Microsoft.EntityFrameworkCore;
using SalonBooking.Application.Interfaces;
using SalonBooking.Persistence.Configurations;
using SalonBooking.Domain.Entities;

//using Microsoft.EntityFrameworkCore;
using SalonBooking.Domain.Common;

namespace SalonBooking.Persistence.Context;

public class SalonBookingDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;
    private readonly long _tenantId;
    private readonly long _branchId;
    private readonly bool _filterByBranch;

    public SalonBookingDbContext(
        DbContextOptions<SalonBookingDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
        _tenantId = currentUserService.TenantId ?? 0;
        _branchId = currentUserService.BranchId ?? 0;
        _filterByBranch = currentUserService.BranchId is > 0;
    }


    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

  public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<DocSeries> DocSeries => Set<DocSeries>();

public DbSet<Employee> Employees => Set<Employee>();

public DbSet<Service> Services => Set<Service>();
public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
public DbSet<EmployeeService> EmployeeServices => Set<EmployeeService>();
public DbSet<Appointment> Appointments => Set<Appointment>();
public DbSet<AppointmentService> AppointmentServices => Set<AppointmentService>();
public DbSet<AppointmentStatusHistory> AppointmentStatusHistories => Set<AppointmentStatusHistory>();
public DbSet<AppointmentServiceScheduleHistory> AppointmentServiceScheduleHistories => Set<AppointmentServiceScheduleHistory>();
public DbSet<AppointmentServiceStatusHistory> AppointmentServiceStatusHistories => Set<AppointmentServiceStatusHistory>();
public DbSet<StaffSchedule> StaffSchedules => Set<StaffSchedule>();
public DbSet<StaffLeave> StaffLeaves => Set<StaffLeave>();
public DbSet<Qualification> Qualifications => Set<Qualification>();
public DbSet<EmployeeQualification> EmployeeQualifications => Set<EmployeeQualification>();
public DbSet<RetailProduct> RetailProducts => Set<RetailProduct>();
public DbSet<TaxSetting> TaxSettings => Set<TaxSetting>();
public DbSet<Bill> Bills => Set<Bill>();
public DbSet<BillLine> BillLines => Set<BillLine>();
public DbSet<ActiveServiceCharge> ActiveServiceCharges => Set<ActiveServiceCharge>();
public DbSet<BillPayment> BillPayments => Set<BillPayment>();
public DbSet<RentalAsset> RentalAssets => Set<RentalAsset>();
public DbSet<RentalReservation> RentalReservations => Set<RentalReservation>();
public DbSet<ActiveRentalCharge> ActiveRentalCharges => Set<ActiveRentalCharge>();
public DbSet<ServiceFeedback> ServiceFeedback => Set<ServiceFeedback>();

    //public DbSet<Branch> Branches { get; set; }
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.ApplyConfiguration(new CustomerConfiguration());
    modelBuilder.ApplyConfiguration(new DocSeriesConfiguration());
    modelBuilder.ApplyConfiguration(new EmployeeConfiguration());
    modelBuilder.ApplyConfiguration(new UserConfiguration());
    modelBuilder.ApplyConfiguration(new RoleConfiguration());
    modelBuilder.ApplyConfiguration(new PermissionConfiguration());
    modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
    modelBuilder.ApplyConfiguration(new RolePermissionConfiguration());
    modelBuilder.ApplyConfiguration(new TenantConfiguration());
    modelBuilder.ApplyConfiguration(new BranchConfiguration());

        modelBuilder.ApplyConfiguration(new ServiceCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceConfiguration());
        modelBuilder.ApplyConfiguration(new EmployeeServiceConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentServiceConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentStatusHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentServiceScheduleHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentServiceStatusHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new StaffScheduleConfiguration());
        modelBuilder.ApplyConfiguration(new StaffLeaveConfiguration());
        modelBuilder.ApplyConfiguration(new QualificationConfiguration());
        modelBuilder.ApplyConfiguration(new EmployeeQualificationConfiguration());
        modelBuilder.ApplyConfiguration(new RetailProductConfiguration());
        modelBuilder.ApplyConfiguration(new TaxSettingConfiguration());
        modelBuilder.ApplyConfiguration(new BillConfiguration());
        modelBuilder.ApplyConfiguration(new BillLineConfiguration());
        modelBuilder.ApplyConfiguration(new ActiveServiceChargeConfiguration());
        modelBuilder.ApplyConfiguration(new BillPaymentConfiguration());
        modelBuilder.ApplyConfiguration(new RentalAssetConfiguration());
        modelBuilder.ApplyConfiguration(new RentalReservationConfiguration());
        modelBuilder.ApplyConfiguration(new ActiveRentalChargeConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceFeedbackConfiguration());

        var rowVersion = modelBuilder.Entity<AppointmentService>().Property(x => x.RowVersion);
        var billVersion = modelBuilder.Entity<Bill>().Property(x => x.RowVersion);
        if (Database.IsSqlServer())
        {
            rowVersion.IsRowVersion();
            billVersion.IsRowVersion();
        }

    modelBuilder.Entity<Customer>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<DocSeries>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId);

    modelBuilder.Entity<Employee>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<Service>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<ServiceCategory>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId);

    modelBuilder.Entity<Branch>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<User>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<Role>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId);

    modelBuilder.Entity<Tenant>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId);

    modelBuilder.Entity<EmployeeService>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<Appointment>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<AppointmentService>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<AppointmentStatusHistory>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<AppointmentServiceScheduleHistory>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<AppointmentServiceStatusHistory>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<StaffSchedule>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<StaffLeave>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<Qualification>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId);

    modelBuilder.Entity<EmployeeQualification>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId);

    modelBuilder.Entity<RetailProduct>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<TaxSetting>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<Bill>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<BillLine>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<BillPayment>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<ActiveServiceCharge>()
        .HasQueryFilter(x =>
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<RentalAsset>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<RentalReservation>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<ActiveRentalCharge>()
        .HasQueryFilter(x =>
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

    modelBuilder.Entity<ServiceFeedback>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));
}

public override async Task<int> SaveChangesAsync(
    CancellationToken cancellationToken = default)
{
        var utcNow = DateTime.UtcNow;
        var userId = _currentUserService.UserId;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedDate = utcNow;
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.IsDeleted = false;
                    entry.Entity.IsActive = true;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedDate = utcNow;
                    entry.Entity.ModifiedBy = userId;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.ModifiedBy = userId;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.ModifiedDate = utcNow;
                    break;
            }
        }
    return await base.SaveChangesAsync(cancellationToken);
}
}