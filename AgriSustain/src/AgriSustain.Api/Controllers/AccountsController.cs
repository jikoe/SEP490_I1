using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountManagementService _accountManagementService;

    public AccountsController(IAccountManagementService accountManagementService)
    {
        _accountManagementService = accountManagementService;
    }

    [HttpPut("{accountId:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateAccount(long accountId, [FromBody] UpdateAccountRequestDto request, CancellationToken cancellationToken)
    {
        await _accountManagementService.UpdateAsync(accountId, request, cancellationToken);
        return NoContent();
    }
}
