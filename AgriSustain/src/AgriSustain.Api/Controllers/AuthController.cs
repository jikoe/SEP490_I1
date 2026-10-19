using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthenticationService service) : ControllerBase
{
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.InitiateRegistrationAsync(request, cancellationToken), HttpContext);

    [HttpPost("verify-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp(
        [FromBody] VerifyOtpRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.VerifyOtpAndCompleteRegistrationAsync(request, cancellationToken), HttpContext);

    [HttpPost("resend-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendOtp(
        [FromBody] ResendOtpRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.ResendOtpAsync(request, cancellationToken), HttpContext);
}
