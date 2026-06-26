using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

using SalonBooking.Application.Common.Settings;
using SalonBooking.Application.Interfaces;
using SalonBooking.Domain.Entities;

namespace SalonBooking.Infrastructure.Authentication;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _jwtSettings;

    public JwtTokenService(
        IOptions<JwtSettings> jwtOptions)
    {
        _jwtSettings = jwtOptions.Value;
    }

    public string GenerateToken(User user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,
                user.UserId.ToString()),

            new(JwtRegisteredClaimNames.UniqueName,
                user.Username),

            new(ClaimTypes.NameIdentifier,
                user.UserId.ToString()),

            new(ClaimTypes.Name,
                user.Username)
        };

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(
                _jwtSettings.SecretKey));

        var credentials =
            new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

        var expires =
            DateTime.UtcNow.AddMinutes(
                _jwtSettings.ExpiryMinutes);

        var token =
            new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: expires,
                signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }
}