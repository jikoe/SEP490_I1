using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Route("api/passwords")]
[Authorize(AuthenticationSchemes = "Bearer")]
[EnableRateLimiting("Credentials")]
public sealed class PasswordsController(IPasswordChangeService service) : ControllerBase
{
    [HttpPost("change")]
    public async Task<IActionResult> Change(
        ChangePasswordRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.ChangeAsync(request, cancellationToken), HttpContext);
}
