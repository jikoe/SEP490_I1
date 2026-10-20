using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Services.Shared;
using AgriSustain.Application.Services.Farmer;
using AgriSustain.Application.Services.RetailCustomer;
using AgriSustain.Infrastructure.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using D = AgriSustain.Infrastructure.Data.Entities;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

internal sealed partial class FarmerRetailRepository
{
    public Task<long?> SubmissionAsync(long actorId, Guid key) => db.Set<D.ProductSubmission>()
        .Where(x => x.ActorId == actorId && x.RequestKey == key).Select(x => (long?)x.ProductId).SingleOrDefaultAsync(ct);
    public Task RecordSubmissionAsync(long actorId, Guid key, long productId)
    {
        db.Add(new D.ProductSubmission {ActorId = actorId, RequestKey = key, ProductId = productId});
        return Task.CompletedTask;
    }
    public async Task<FarmerRetailActor?> ActorAsync(long id)
    {
        var u = await db.User.Include(x => x.FarmerProfile).SingleOrDefaultAsync(x => x.UserId == id, ct);
        return u is null ? null : new(u.UserId, u.Role, u.Status, u.FarmerProfile?.FarmerProfileId,
            u.FarmerProfile?.VerificationStatus == "Approved", u.LockedUntil > Now);
    }
    public Task<CategoryOption[]> CategoriesAsync() => db.Category.Where(x => x.IsActive).OrderBy(x => x.CategoryName)
        .Select(x => new CategoryOption(x.CategoryId, x.CategoryName)).ToArrayAsync(ct);
    public Task<bool> CategoryActiveAsync(long id) => db.Category.AnyAsync(x => x.CategoryId == id && x.IsActive, ct);
    private IQueryable<D.Product> ProductGraph() => db.Product.Include(x => x.Category).Include(x => x.FarmerProfile).ThenInclude(x => x.User)
        .Include(x => x.ProductImage).Include(x => x.HarvestBatch).AsSplitQuery();
    public async Task<PagedResponse<Listing>> ListingsAsync(long? farmId, FarmerRetailQuery query)
    {
        var q = ProductGraph();
        q = farmId.HasValue ? q.Where(p => p.FarmerProfileId == farmId) : q.Where(p => p.ApprovalStatus == "Approved" && p.IsPublished
            && p.Category.IsActive && p.FarmerProfile.VerificationStatus == "Approved" && p.FarmerProfile.User.Status == "Active"
            && (p.FarmerProfile.User.LockedUntil == null || p.FarmerProfile.User.LockedUntil <= Now));
        if (!string.IsNullOrWhiteSpace(query.Keyword)) q = q.Where(p => p.ProductName.Contains(query.Keyword));
        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(p => p.ApprovalStatus == query.Status);
        if (query.Published.HasValue) q = q.Where(p => p.IsPublished == query.Published);
        if (query.CategoryId.HasValue) q = q.Where(p => p.CategoryId == query.CategoryId);
        if (query.From.HasValue) q = q.Where(p => p.CreatedAt >= query.From.Value.Date.AddHours(-7));
        if (query.To.HasValue) q = q.Where(p => p.CreatedAt < query.To.Value.Date.AddDays(1).AddHours(-7));
        int count = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(p => p.ProductId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(rows.Select(Map).ToArray(), count, query.Page, query.PageSize);
    }
    public async Task<Listing?> ListingAsync(long id)
    {
        var p = await ProductGraph().SingleOrDefaultAsync(x => x.ProductId == id, ct);
        if (p is null) return null;
        var view = Map(p);
        view.UnitLocked = p.HarvestBatch.Count > 0 || await db.OrderItem.AnyAsync(x => x.ProductId == id, ct);
        return view;
    }
    private static BatchOffer Batch(D.HarvestBatch b) => new(b.HarvestBatchId, b.ProductId, b.BatchCode,
        b.AvailableQuantity, b.ReservedQuantity, b.Status, b.ActualHarvestDate, b.ExpirationDate, b.StockEnteredAt is not null);
    private Listing Map(D.Product p) => new() {
        Id = p.ProductId, FarmId = p.FarmerProfileId, FarmName = p.FarmerProfile.FarmName, Name = p.ProductName,
        CategoryId = p.CategoryId, CategoryActive = p.Category.IsActive, Unit = p.Unit, QuantityStep = p.QuantityStep,
        Origin = p.OriginProvince ?? "", Description = p.Description ?? "", RetailPrice = p.BaseRetailPrice,
        WholesaleMinimum = p.WholesaleMinQuantity, WholesalePrice = p.WholesalePrice, Approval = p.ApprovalStatus,
        Published = p.IsPublished, FarmEligible = p.FarmerProfile.VerificationStatus == "Approved" && p.FarmerProfile.User.Status == "Active"
            && (p.FarmerProfile.User.LockedUntil is null || p.FarmerProfile.User.LockedUntil <= Now),
        RejectionReason = p.RejectionReason, Revision = p.Revision,
        Images = p.ProductImage.OrderBy(x => x.DisplayOrder).Select(x => new ImageView(x.ImageUrl, x.IsPrimary)).ToArray(),
        Batches = p.HarvestBatch.Select(Batch).ToArray(),
        AvailableQuantity = p.HarvestBatch.Where(b => FarmerRetailRules.BatchEligible(Batch(b), Today)).Sum(b => b.AvailableQuantity)
    };
    public async Task<long> SaveListingAsync(Listing p, ImageInput[]? images, long actorId, string action)
    {
        var row = p.Id == 0 ? new D.Product { FarmerProfileId = p.FarmId, CreatedAt = Now } : await db.Product.FindAsync([p.Id], ct)
            ?? throw new DbUpdateConcurrencyException();
        if (p.Id == 0) db.Product.Add(row);
        string before = JsonSerializer.Serialize(new {row.ProductName, row.ApprovalStatus, row.IsPublished, row.BaseRetailPrice, row.Revision});
        row.ProductName = p.Name; row.CategoryId = p.CategoryId; row.Unit = p.Unit; row.OriginProvince = p.Origin;
        row.Description = p.Description; row.BaseRetailPrice = p.RetailPrice; row.WholesaleMinQuantity = p.WholesaleMinimum;
        row.WholesalePrice = p.WholesalePrice; row.ApprovalStatus = p.Approval; row.IsPublished = p.Published;
        row.QuantityStep = p.QuantityStep; row.Revision = p.Revision;
        row.RejectionReason = p.RejectionReason;
        if (images is not null)
        {
            var old = await db.ProductImage.Where(x => x.ProductId == p.Id).ToArrayAsync(ct);
            db.ProductImage.RemoveRange(old);
            int index = 0;
            foreach (var image in images)
            {
                string url = image.Content is null ? image.ExistingUrl! : Media(image, actorId, true);
                db.ProductImage.Add(new D.ProductImage { Product = row, ImageUrl = url, DisplayOrder = index++, IsPrimary = image.Primary, AltText = p.Name });
            }
            // Old media is removed only when no retained image references it; rollback restores it.
            foreach (var oldImage in old.Where(o => !images.Any(i => i.ExistingUrl == o.ImageUrl)))
                if (Guid.TryParse(oldImage.ImageUrl.Split('/').Last(), out var mediaId))
                {
                    var media = await db.Set<D.StoredMedia>().FindAsync([mediaId], ct);
                    if (media?.OwnerUserId == actorId) db.Remove(media);
                }
        }
        await db.SaveChangesAsync(ct); // Generates ID inside the transaction, not a partial commit.
        Audit(actorId, "Product", row.ProductId, action, before, JsonSerializer.Serialize(new {p.Approval, p.Published, p.RetailPrice, p.Revision}));
        if (action is "Approve" or "Reject")
        {
            long owner = await db.FarmerProfile.Where(f => f.FarmerProfileId == row.FarmerProfileId).Select(f => f.UserId).SingleAsync(ct);
            Notify(owner, action == "Approve" ? "MSG29" : "MSG165", row.ProductName + ": " + p.Approval + (p.RejectionReason is null ? "" : ": " + p.RejectionReason));
        }
        return row.ProductId;
    }
    private string Media(ImageInput input, long owner, bool publicImage)
    {
        var id = Guid.NewGuid();
        db.Add(new D.StoredMedia { Id = id, OwnerUserId = owner, Content = input.Content!, ContentType = input.ContentType, PublicProductImage = publicImage });
        return "/api/product-media/" + id;
    }
}
