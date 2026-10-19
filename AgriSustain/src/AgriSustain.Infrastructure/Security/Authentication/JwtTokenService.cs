using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Domain.Entities.Accounts;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AgriSustain.Infrastructure.Security.Authentication;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _jwtOptions;

    public JwtTokenService(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public string CreateAccessToken(Account account)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Name, account.FullName),
            new(ClaimTypes.Role, account.Role)
        };

        return CreateToken(
            claims,
            _jwtOptions.Audience,
            DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes));
    }

    public string CreateRefreshToken(Account account)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new Claim("token_type", "refresh")
        };

        return CreateToken(
            claims,
            $"{_jwtOptions.Audience}-refresh",
            DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays));
    }

    public long ValidateRefreshToken(string token)
    {
        try
        {
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = $"{_jwtOptions.Audience}-refresh",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey)),
                ValidateLifetime = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.FromSeconds(30)
            }, out _);

            var accountId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (principal.FindFirst("token_type")?.Value != "refresh" ||
                !long.TryParse(accountId, out var parsedAccountId))
            {
                throw new UnauthorizedAccessException("Refresh token is invalid.");
            }

            return parsedAccountId;
        }
        catch (SecurityTokenException exception)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.", exception);
        }
        catch (ArgumentException exception)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or expired.", exception);
        }
    }

    private string CreateToken(IEnumerable<Claim> claims, string audience, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
