

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

public DbSet<Employee> Employees => Set<Employee>();

public DbSet<Service> Services => Set<Service>();
public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
public DbSet<EmployeeService> EmployeeServices => Set<EmployeeService>();
public DbSet<Appointment> Appointments => Set<Appointment>();
public DbSet<AppointmentService> AppointmentServices => Set<AppointmentService>();
public DbSet<AppointmentStatusHistory> AppointmentStatusHistories => Set<AppointmentStatusHistory>();
public DbSet<AppointmentServiceScheduleHistory> AppointmentServiceScheduleHistories => Set<AppointmentServiceScheduleHistory>();
public DbSet<StaffSchedule> StaffSchedules => Set<StaffSchedule>();
public DbSet<StaffLeave> StaffLeaves => Set<StaffLeave>();

    //public DbSet<Branch> Branches { get; set; }
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    modelBuilder.ApplyConfiguration(new CustomerConfiguration());
    modelBuilder.ApplyConfiguration(new EmployeeConfiguration());
    modelBuilder.ApplyConfiguration(new TenantConfiguration());
    modelBuilder.ApplyConfiguration(new BranchConfiguration());

        modelBuilder.ApplyConfiguration(new ServiceCategoryConfiguration());
        modelBuilder.ApplyConfiguration(new ServiceConfiguration());
        modelBuilder.ApplyConfiguration(new EmployeeServiceConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentServiceConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentStatusHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new AppointmentServiceScheduleHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new StaffScheduleConfiguration());
        modelBuilder.ApplyConfiguration(new StaffLeaveConfiguration());

        var rowVersion = modelBuilder.Entity<AppointmentService>().Property(x => x.RowVersion);
        if (Database.IsSqlServer())
        {
            rowVersion.IsRowVersion();
        }

    modelBuilder.Entity<Customer>()
        .HasQueryFilter(x =>
            !x.IsDeleted &&
            x.TenantId == _tenantId &&
            (!_filterByBranch || x.BranchId == _branchId));

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