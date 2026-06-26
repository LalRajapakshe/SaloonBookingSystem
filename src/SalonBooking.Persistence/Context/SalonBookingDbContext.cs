

using Microsoft.EntityFrameworkCore;

using SalonBooking.Domain.Entities;

namespace SalonBooking.Persistence.Context;

public class SalonBookingDbContext : DbContext
{
    public SalonBookingDbContext(
        DbContextOptions<SalonBookingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Branch> Branches => Set<Branch>();

    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<Customer> Customers { get; set; }
}