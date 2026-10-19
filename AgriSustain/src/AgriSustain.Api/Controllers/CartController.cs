using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Route("api/carts/current")]
[Authorize(AuthenticationSchemes = "Bearer", Roles = "RetailCustomer")]
public sealed class CartController(ICartService service) : ControllerBase
{
    [HttpPost("items")]
    public async Task<IActionResult> Add(
        AddCartItemRequest request, CancellationToken cancellationToken) =>
        ApiResults.From(await service.AddAsync(request, cancellationToken), HttpContext);
}
