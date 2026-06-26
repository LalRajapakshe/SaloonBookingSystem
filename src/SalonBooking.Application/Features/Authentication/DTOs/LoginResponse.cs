namespace SalonBooking.Application.Features.Authentication.DTOs;

public class LoginResponse
{
    public string AccessToken { get; set; }
        = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public string Username { get; set; }
        = string.Empty;

    public string FullName { get; set; }
        = string.Empty;
}
