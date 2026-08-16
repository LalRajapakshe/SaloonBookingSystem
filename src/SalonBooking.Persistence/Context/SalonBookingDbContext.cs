

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
    public SalonBookingDbContext(
        DbContextOptions<SalonBookingDbContext> options, ICurrentUserService currentUserService)
        : base(options)
    {
        _currentUserService = currentUserService;
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

    // Global Query Filters will go here

      modelBuilder.Entity<Customer>()
        .HasQueryFilter(x => !x.IsDeleted);

    modelBuilder.Entity<Employee>()
        .HasQueryFilter(x => !x.IsDeleted);

    modelBuilder.Entity<Branch>()
        .HasQueryFilter(x => !x.IsDeleted);

    modelBuilder.Entity<Tenant>()
        .HasQueryFilter(x => !x.IsDeleted);
}

public override async Task<int> SaveChangesAsync(
    CancellationToken cancellationToken = default)
{
        var utcNow = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedDate = utcNow;
                    entry.Entity.CreatedBy = _currentUserService.UserId;
                    entry.Entity.IsDeleted = false;
                    entry.Entity.IsActive = true;
                    break;

                case EntityState.Modified:
                    entry.Entity.ModifiedDate = utcNow;
                    entry.Entity.ModifiedBy = _currentUserService.UserId;
                    break;

                case EntityState.Deleted:
                    entry.State = EntityState.Modified;
                    entry.Entity.ModifiedBy = _currentUserService.UserId;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.ModifiedDate = utcNow;
                    break;
            }
        }
    return await base.SaveChangesAsync(cancellationToken);
}
}