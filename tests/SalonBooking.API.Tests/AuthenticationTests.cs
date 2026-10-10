using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;

namespace SalonBooking.API.Tests;

[Collection(ApiCollection.Name)]
public class AuthenticationTests
{
    private readonly ApiFactory _factory;

    public AuthenticationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Valid_active_user_receives_token_and_expiry()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/Auth/login",
            new { username = ApiFactory.BranchUserAName, password = ApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal(ApiFactory.BranchUserAName, body.Username);
        Assert.True(body.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Wrong_password_returns_401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/Auth/login",
            new { username = ApiFactory.BranchUserAName, password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_username_returns_401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/Auth/login",
            new { username = "nobody", password = ApiFactory.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Inactive_user_returns_401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/Auth/login",
            new { username = ApiFactory.InactiveUserName, password = ApiFactory.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Branch_user_token_contains_identity_tenant_role_and_branch()
    {
        var login = await _factory.LoginAsync(ApiFactory.BranchUserAName);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);

        Assert.Equal(_factory.BranchUserAId.ToString(), Claim(token, "sub"));
        Assert.Equal(ApiFactory.BranchUserAName, Claim(token, "unique_name"));
        Assert.Equal(_factory.TenantAId.ToString(), Claim(token, "TenantId"));
        Assert.Equal("Staff", Claim(token, ClaimTypes.Role));
        Assert.Equal(_factory.BranchA1Id.ToString(), Claim(token, "BranchId"));
    }

    [Fact]
    public async Task Tenant_wide_user_token_has_no_branch_claim()
    {
        var login = await _factory.LoginAsync(ApiFactory.TenantUserAName);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);

        Assert.Equal(_factory.TenantUserAId.ToString(), Claim(token, "sub"));
        Assert.Equal(ApiFactory.TenantUserAName, Claim(token, "unique_name"));
        Assert.Equal(_factory.TenantAId.ToString(), Claim(token, "TenantId"));
        Assert.Equal("Admin", Claim(token, ClaimTypes.Role));
        Assert.DoesNotContain(token.Claims, claim => claim.Type == "BranchId");
    }

    private static string Claim(JwtSecurityToken token, string type)
    {
        return token.Claims.Single(claim => claim.Type == type).Value;
    }
}
