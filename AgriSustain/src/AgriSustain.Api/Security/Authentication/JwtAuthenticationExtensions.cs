using System.Security.Cryptography;
using System.Text;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.Security.Authentication;
using AgriSustain.Infrastructure.Services.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AgriSustain.Api.Security.Authentication;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddAccountAuthentication(
        this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        // Local fallback is process-scoped; persistent sessions require Jwt:SecretKey in secrets.
        string localKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        services.AddOptions<JwtOptions>().Bind(configuration.GetSection(JwtOptions.SectionName))
            .PostConfigure(options =>
            {
                if (isDevelopment && string.IsNullOrEmpty(options.SecretKey))
                {
                    options.SecretKey = localKey;
                }
            })
            .Validate(options => Encoding.UTF8.GetByteCount(options.SecretKey) >= 32
                && !string.IsNullOrWhiteSpace(options.Issuer)
                && !string.IsNullOrWhiteSpace(options.Audience)
                && options.AccessTokenExpirationMinutes > 0
                && options.RefreshTokenExpirationDays > 0, "Invalid JWT configuration.")
            .ValidateOnStart();
        services.AddDbContext<AppDbContext>((provider, options) => options.UseSqlServer(
            provider.GetRequiredService<IConfiguration>().GetConnectionString("AgriSustain")));
        services.AddScoped<IIdentityAccountRepository, AccountRepository>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddAuthentication().AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, configured) =>
            {
                JwtOptions jwt = configured.Value;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidateLifetime = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });
        return services;
    }
}
