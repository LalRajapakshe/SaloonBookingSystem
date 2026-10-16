namespace SalonBooking.Infrastructure.Authentication;

public static class DevelopmentAdminSeeder
{
    public static Task SeedAsync(IServiceProvider services)
    {
        return AccessControlBootstrap.EnsureAsync(services);
    }
}
