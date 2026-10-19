using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Domain.Entities.Accounts;
using Microsoft.Extensions.Options;

namespace AgriSustain.Infrastructure.Services.Authentication;

public class AuthService : IAuthService
{
    private readonly IIdentityAccountRepository _accountRepository;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(
        IIdentityAccountRepository accountRepository,
        IJwtTokenService jwtTokenService,
        IOptions<JwtOptions> jwtOptions,
        IPasswordHasher passwordHasher)
    {
        _accountRepository = accountRepository;
        _jwtTokenService = jwtTokenService;
        _jwtOptions = jwtOptions.Value;
        _passwordHasher = passwordHasher;
    }

    public async Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedAccessException("Email and password are required.");
        }

        var account = await _accountRepository.GetByEmailAsync(request.Email.Trim(), cancellationToken);
        if (account is null ||
            !account.IsActive ||
            account.LockedUntil > DateTime.UtcNow ||
            !_passwordHasher.Verify(request.Password, account.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var accessToken = _jwtTokenService.CreateAccessToken(account);
        var refreshTokenValue = _jwtTokenService.CreateRefreshToken(account);
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

        return new LoginResponseDto
        {
            AccountId = account.Id,
            Email = account.Email,
            FullName = account.FullName,
            Role = account.Role,
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            RefreshTokenExpiresAt = refreshTokenExpiresAt
        };
    }

    public async Task<LoginResponseDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Refresh token is required.");
        }

        var accountId = _jwtTokenService.ValidateRefreshToken(refreshToken.Trim());
        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account is null || !account.IsActive || account.LockedUntil > DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Account is inactive or unavailable.");
        }

        var newAccessToken = _jwtTokenService.CreateAccessToken(account);
        var newRefreshTokenValue = _jwtTokenService.CreateRefreshToken(account);
        var newRefreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

        return new LoginResponseDto
        {
            AccountId = account.Id,
            Email = account.Email,
            FullName = account.FullName,
            Role = account.Role,
            AccessToken = newAccessToken,
            RefreshToken = newRefreshTokenValue,
            AccessTokenExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenExpirationMinutes),
            RefreshTokenExpiresAt = newRefreshTokenExpiresAt
        };
    }

}
