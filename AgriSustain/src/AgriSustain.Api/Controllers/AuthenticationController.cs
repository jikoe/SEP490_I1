using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthenticationController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly JwtOptions _jwtOptions;

    public AuthenticationController(IAuthService authService, IOptions<JwtOptions> jwtOptions)
    {
        _authService = authService;
        _jwtOptions = jwtOptions.Value;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken cancellationToken)
    {
        LoginResponseDto response;
        try
        {
            response = await _authService.LoginAsync(request, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiresAt);

        return Ok(new
        {
            accountId = response.AccountId,
            email = response.Email,
            fullName = response.FullName,
            role = response.Role,
            accessToken = response.AccessToken,
            accessTokenExpiresAt = response.AccessTokenExpiresAt,
            refreshTokenExpiresAt = response.RefreshTokenExpiresAt
        });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto? request, CancellationToken cancellationToken)
    {
        var refreshToken = request?.RefreshToken ?? Request.Cookies["refreshToken"];
        LoginResponseDto response;
        try
        {
            response = await _authService.RefreshAsync(refreshToken ?? string.Empty, cancellationToken);
        }
        catch (UnauthorizedAccessException)
        {
            Response.Cookies.Delete("refreshToken");
            return Unauthorized(new { message = "Refresh token is invalid or expired." });
        }

        SetRefreshTokenCookie(response.RefreshToken, response.RefreshTokenExpiresAt);

        return Ok(new
        {
            accountId = response.AccountId,
            email = response.Email,
            fullName = response.FullName,
            role = response.Role,
            accessToken = response.AccessToken,
            accessTokenExpiresAt = response.AccessTokenExpiresAt,
            refreshTokenExpiresAt = response.RefreshTokenExpiresAt
        });
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("refreshToken");
        return Ok(new { message = "Logged out successfully." });
    }

    private void SetRefreshTokenCookie(string refreshToken, DateTime expiresAt)
    {
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = false,
            SameSite = SameSiteMode.Lax,
            Expires = expiresAt,
            IsEssential = true,
            Path = "/"
        });
    }
}
