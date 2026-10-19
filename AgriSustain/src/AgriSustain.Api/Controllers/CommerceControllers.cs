using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Commerce;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Services.Commerce;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Api.Controllers;

public sealed record VisibilityInput(bool Published, long Revision, bool Confirmed);
public sealed record QuantityInput(decimal Quantity);
[ApiController, Route("api/moderation/products"), Authorize(Roles = "ContentModerator")]
public sealed class ProductModerationController(ProductService service) : ControllerBase
{
    [HttpPost("{id:long}")]
    public async Task<IActionResult> Moderate(long id, ModerationInput input, CancellationToken ct) => ApiResults.From(await service.ModerateAsync(id, input, ct), HttpContext);
}

[ApiController, Route("api/farmer/products"), Authorize(Roles = "Farmer")]
[RequestSizeLimit(72 * 1024 * 1024)]
public sealed class FarmerProductsController(ProductService service, ICommerceUnitOfWork unit, ICurrentUser user, IAddressCatalog addresses) : ControllerBase
{
    [HttpGet("provinces")] public IActionResult Provinces() => Ok(addresses.GetProvinces());
    [HttpGet] public async Task<IActionResult> List([FromQuery] CommerceQuery q, CancellationToken ct) => ApiResults.From(await service.GetListAsync(q, ct), HttpContext);
    [HttpGet("categories")] public async Task<IActionResult> Categories(CancellationToken ct) => ApiResults.From(await unit.RunAsync(async r => {
        var actor = await CommerceRules.Actor(r, user, "Farmer");
        return actor.IsSuccess ? Result<CategoryOption[]>.Success(await r.CategoriesAsync()) : Result<CategoryOption[]>.Failure(actor.Error!);
    }, ct), HttpContext);
    [HttpGet("{id:long}")] public async Task<IActionResult> Get(long id, CancellationToken ct) => ApiResults.From(await service.GetAsync(id, ct), HttpContext);
    [HttpPost] public async Task<IActionResult> Submit(ProductInput input, CancellationToken ct) => ApiResults.From(await service.SubmitAsync(input, ct), HttpContext);
    [HttpPut("{id:long}")] public async Task<IActionResult> Update(long id, ProductInput input, CancellationToken ct) => ApiResults.From(await service.UpdateAsync(id, input, ct), HttpContext);
    [HttpPost("{id:long}/visibility")] public async Task<IActionResult> Visibility(long id, VisibilityInput input, CancellationToken ct) => ApiResults.From(await service.SetVisibilityAsync(id, input.Published, input.Revision, input.Confirmed, ct), HttpContext);
}
[ApiController, Route("api/cart"), Authorize(Roles = "RetailCustomer")]
public sealed class CartsController(CartService service, ICommerceUnitOfWork unit, ICurrentUser user) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> Get(CancellationToken ct) => ApiResults.From(await service.GetAsync(ct), HttpContext);
    [HttpGet("offers")] public async Task<IActionResult> Offers([FromQuery] CommerceQuery q, CancellationToken ct) => ApiResults.From(await unit.RunAsync(async r => {
        var actor = await CommerceRules.Actor(r, user, "RetailCustomer");
        if (!actor.IsSuccess) return Result<PagedResponse<Listing>>.Failure(actor.Error!);
        if (CommerceRules.Query(q) is { } error) return Result<PagedResponse<Listing>>.Failure(error);
        return Result<PagedResponse<Listing>>.Success(await r.ListingsAsync(null, q));
    }, ct), HttpContext);
    [HttpPost("items")] public async Task<IActionResult> Add(AddCartItem input, CancellationToken ct) => ApiResults.From(await service.AddItemAsync(input, ct), HttpContext);
    [HttpPut("items/{id:long}")] public async Task<IActionResult> Quantity(long id, QuantityInput input, CancellationToken ct) => ApiResults.From(await service.UpdateQuantityAsync(id, input.Quantity, ct), HttpContext);
    [HttpPost("selection")] public async Task<IActionResult> Select(CartSelection input, CancellationToken ct) => ApiResults.From(await service.SetSelectionAsync(input, ct), HttpContext);
    [HttpPost("remove")] public async Task<IActionResult> Remove(RemoveCartItems input, CancellationToken ct) => ApiResults.From(await service.RemoveItemsAsync(input, ct), HttpContext);
    [HttpPost("prepare-checkout")] public async Task<IActionResult> Prepare(CheckoutReview input, CancellationToken ct) => ApiResults.From(await service.PrepareCheckoutAsync(input.ReviewToken, ct), HttpContext);
}
[ApiController, Route("api/seller-orders"), Authorize]
[RequestSizeLimit(72 * 1024 * 1024)]
public sealed class SellerOrdersController(OrderFulfillmentService service) : ControllerBase
{
    [HttpGet, Authorize(Roles = "Farmer")] public async Task<IActionResult> List([FromQuery] CommerceQuery q, CancellationToken ct) => ApiResults.From(await service.GetListAsync(q, false, ct), HttpContext);
    [HttpGet("mine"), Authorize(Roles = "RetailCustomer")] public async Task<IActionResult> Mine([FromQuery] CommerceQuery q, CancellationToken ct) => ApiResults.From(await service.GetListAsync(q, true, ct), HttpContext);
    [HttpGet("{id:long}"), Authorize(Roles = "Farmer,RetailCustomer")] public async Task<IActionResult> Get(long id, CancellationToken ct) => ApiResults.From(await service.GetAsync(id, User.IsInRole("RetailCustomer"), ct), HttpContext);
    [HttpPost("{id:long}/confirm"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Confirm(long id, CancellationToken ct) => ApiResults.From(await service.ConfirmAsync(id, ct), HttpContext);
    [HttpPost("{id:long}/ready"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Ready(long id, CancellationToken ct) => ApiResults.From(await service.MarkReadyAsync(id, ct), HttpContext);
    [HttpPost("{id:long}/dispatch"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Dispatch(long id, CancellationToken ct) => ApiResults.From(await service.DispatchAsync(id, ct), HttpContext);
    [HttpPost("{id:long}/reject"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Reject(long id, OrderAction input, CancellationToken ct) => ApiResults.From(await service.RejectAsync(id, input, ct), HttpContext);
    [HttpPost("{id:long}/receive"), Authorize(Roles = "RetailCustomer")] public async Task<IActionResult> Receive(long id, CancellationToken ct) => ApiResults.From(await service.ReceiveAsync(id, false, ct), HttpContext);
    [HttpPost("{id:long}/accept"), Authorize(Roles = "RetailCustomer")] public async Task<IActionResult> Accept(long id, CancellationToken ct) => ApiResults.From(await service.ReceiveAsync(id, true, ct), HttpContext);
    [HttpPost("{id:long}/delivery-issue"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Issue(long id, EvidenceInput input, CancellationToken ct) => ApiResults.From(await service.EvidenceAsync(id, input, true, ct), HttpContext);
    [HttpPost("{id:long}/evidence"), Authorize(Roles = "Farmer")] public async Task<IActionResult> Evidence(long id, EvidenceInput input, CancellationToken ct) => ApiResults.From(await service.EvidenceAsync(id, input, false, ct), HttpContext);
}
[ApiController, Route("api/product-media")]
public sealed class ProductMediaController(AgriSustainDbContext db, ICurrentUser user, TimeProvider clock) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var media = await db.Set<CommerceMedia>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (media is null) return NotFound();
        var now = clock.GetUtcNow().UtcDateTime;
        bool owner = user.UserId is { } actorId && media.OwnerUserId == actorId && await db.User.AnyAsync(x => x.UserId == actorId
            && x.Status == "Active" && (x.LockedUntil == null || x.LockedUntil <= now), ct);
        string url = "/api/product-media/" + id;
        bool visible = media.PublicProductImage && await db.ProductImage.AnyAsync(x => x.ImageUrl == url && x.Product.IsPublished
            && x.Product.ApprovalStatus == "Approved" && x.Product.Category.IsActive
            && x.Product.FarmerProfile.VerificationStatus == "Approved" && x.Product.FarmerProfile.User.Status == "Active"
            && (x.Product.FarmerProfile.User.LockedUntil == null || x.Product.FarmerProfile.User.LockedUntil <= now), ct);
        bool reviewer = !media.PublicProductImage && user.UserId is { } reviewerId && await db.DisputeTicket.AnyAsync(t => t.AssignedStaffUserId == reviewerId
            && t.DisputeEvidence.Any(e => e.FileUrl == url), ct);
        if (!owner && !visible && !reviewer) return NotFound();
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.CacheControl = "private, no-store";
        return File(media.Content, media.ContentType);
    }
}
