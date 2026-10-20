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

namespace AgriSustain.Api.Controllers.Shared;


[ApiController, Route("api/product-media")]
public sealed class ProductMediaController(AgriSustainDbContext db, ICurrentUser user, TimeProvider clock) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var media = await db.Set<StoredMedia>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
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
