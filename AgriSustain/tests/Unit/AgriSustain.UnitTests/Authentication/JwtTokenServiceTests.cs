using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Domain.Entities.Accounts;
using AgriSustain.Infrastructure.Security.Authentication;
using Microsoft.Extensions.Options;
using Xunit;

namespace AgriSustain.UnitTests.Authentication;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateAccessToken_ShouldContainUserClaims()
    {
        var jwtOptions = new JwtOptions
        {
            Issuer = "AgriSustain",
            Audience = "AgriSustain-Client",
            SecretKey = "this_is_a_very_long_32_byte_secret_key_12345",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };

        var service = new JwtTokenService(Options.Create(jwtOptions));
        var account = new Account
        {
            Id = 42,
            Email = "admin@agrisustain.vn",
            FullName = "Administrator",
            Role = "Admin",
            IsActive = true
        };

        var token = service.CreateAccessToken(account);
        var payload = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Contains(payload.Claims, claim => claim.Type == ClaimTypes.Email && claim.Value == "admin@agrisustain.vn");
        Assert.Contains(payload.Claims, claim => claim.Type == ClaimTypes.Role && claim.Value == "Admin");
    }

    [Fact]
    public void RefreshToken_ShouldValidateWithoutDatabaseState()
    {
        var jwtOptions = new JwtOptions
        {
            Issuer = "AgriSustain",
            Audience = "AgriSustain-Client",
            SecretKey = "this_is_a_very_long_32_byte_secret_key_12345",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };
        var service = new JwtTokenService(Options.Create(jwtOptions));
        var account = new Account { Id = 42, Role = "WholesaleBuyer" };

        var refreshToken = service.CreateRefreshToken(account);

        Assert.Equal(account.Id, service.ValidateRefreshToken(refreshToken));
    }

    [Fact]
    public void ValidateRefreshToken_ShouldRejectAccessToken()
    {
        var jwtOptions = new JwtOptions
        {
            Issuer = "AgriSustain",
            Audience = "AgriSustain-Client",
            SecretKey = "this_is_a_very_long_32_byte_secret_key_12345",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7
        };
        var service = new JwtTokenService(Options.Create(jwtOptions));
        var account = new Account { Id = 42, Email = "test@example.com", FullName = "Test", Role = "Admin" };

        var accessToken = service.CreateAccessToken(account);

        Assert.Throws<UnauthorizedAccessException>(() => service.ValidateRefreshToken(accessToken));
    }
}
