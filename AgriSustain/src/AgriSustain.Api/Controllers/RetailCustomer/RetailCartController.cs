using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Services.Shared;
using AgriSustain.Application.Services.Farmer;
using AgriSustain.Application.Services.RetailCustomer;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Api.Controllers.RetailCustomer;


public sealed record QuantityInput(decimal Quantity);
[ApiController, Route("api/cart"), Authorize(Roles = "RetailCustomer")]
public sealed class RetailCartController(CartService service, IFarmerRetailUnitOfWork unit, ICurrentUser user) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => ApiResults.From(await service.GetAsync(ct), HttpContext);
    [HttpGet("offers")] public async Task<IActionResult> Offers([FromQuery] FarmerRetailQuery q, CancellationToken ct) => ApiResults.From(await unit.RunAsync(async r => {
        var actor = await FarmerRetailRules.Actor(r, user, "RetailCustomer");
        if (!actor.IsSuccess) return Result<PagedResponse<Listing>>.Failure(actor.Error!);
        if (FarmerRetailRules.Query(q) is { } error) return Result<PagedResponse<Listing>>.Failure(error);
        return Result<PagedResponse<Listing>>.Success(await r.ListingsAsync(null, q));
    }, ct), HttpContext);
    [HttpPost("items")] public async Task<IActionResult> Add(AddCartItem input, CancellationToken ct) => ApiResults.From(await service.AddItemAsync(input, ct), HttpContext);
    [HttpPut("items/{id:long}")] public async Task<IActionResult> Quantity(long id, QuantityInput input, CancellationToken ct) => ApiResults.From(await service.UpdateQuantityAsync(id, input.Quantity, ct), HttpContext);
    [HttpPost("selection")] public async Task<IActionResult> Select(CartSelection input, CancellationToken ct) => ApiResults.From(await service.SetSelectionAsync(input, ct), HttpContext);
    [HttpPost("remove")] public async Task<IActionResult> Remove(RemoveCartItems input, CancellationToken ct) => ApiResults.From(await service.RemoveItemsAsync(input, ct), HttpContext);
    [HttpPost("prepare-checkout")] public async Task<IActionResult> Prepare(CheckoutReview input, CancellationToken ct) => ApiResults.From(await service.PrepareCheckoutAsync(input.ReviewToken, ct), HttpContext);
}
