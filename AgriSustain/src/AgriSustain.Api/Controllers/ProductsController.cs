using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgriSustain.Api.Controllers;

[ApiController]
[Route("api/products")]
[AllowAnonymous]
public sealed class ProductsController(IMarketplaceService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Browse(
        [FromQuery] MarketplaceQuery query, CancellationToken cancellationToken) =>
        ApiResults.From(await service.BrowseAsync(query, cancellationToken), HttpContext);

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken) =>
        ApiResults.From(await service.FindAsync(id, cancellationToken), HttpContext);

    [HttpGet("filters")]
    public async Task<IActionResult> Filters(CancellationToken cancellationToken) =>
        Ok(await service.GetFiltersAsync(cancellationToken));
}
