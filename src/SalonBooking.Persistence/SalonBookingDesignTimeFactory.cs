using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Enums;
using SalonBooking.Persistence.Context;

namespace SalonBooking.Persistence;

/// <summary>
/// Used only by the EF Core tools. The connection string stays in the local file and is not logged.
/// </summary>
public sealed class SalonBookingDesignTimeFactory : IDesignTimeDbContextFactory<SalonBookingDbContext>
{
    public SalonBookingDbContext CreateDbContext(string[] args)
    {
        var connectionString = ReadConnectionString();
        var options = new DbContextOptionsBuilder<SalonBookingDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new SalonBookingDbContext(options, new DesignTimeUser());
    }

    private static string ReadConnectionString()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var candidate = Path.Combine(directory.FullName, "scripts", "ConnectionStr.txt");
            if (File.Exists(candidate))
            {
                var value = File.ReadAllText(candidate).Trim();
                if (value.Length == 0) continue;
                if (value.IndexOf("TrustServerCertificate", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    value += ";TrustServerCertificate=True";
                }

                return value;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            "The local development connection file was not found. The migration was not applied.");
    }

    private sealed class DesignTimeUser : ICurrentUserService
    {
        public bool IsAuthenticated => false;
        public long? UserId => null;
        public long? TenantId => null;
        public long? BranchId => null;
        public string? Username => null;
        public string? Role => null;
        public UserCategory? UserCategory => null;
        public long? EmployeeId => null;
        public long? CustomerId => null;
        public bool IsPlatformAdministrator => false;
    }
}
