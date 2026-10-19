using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

public sealed class MarketplaceRepository(
    AgriSustainDbContext db, TimeProvider clock, ICurrentUser currentUser) : IMarketplaceRepository
{
    public async Task<PagedResponse<ProductSummary>> BrowseAsync(
        MarketplaceQuery query, CancellationToken cancellationToken)
    {
        var products = PublicProducts();
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string keyword = query.Keyword.Trim();
            products = products.Where(product => product.ProductName.Contains(keyword)
                || product.Category.CategoryName.Contains(keyword)
                || product.FarmerProfile.FarmName.Contains(keyword));
        }

        products = products.Where(product =>
            (query.CategoryId == null || product.CategoryId == query.CategoryId)
            && (query.SupplierId == null || product.FarmerProfileId == query.SupplierId)
            && (query.Region == null || product.OriginProvince == query.Region)
            && (query.MinPrice == null || product.BaseRetailPrice >= query.MinPrice)
            && (query.MaxPrice == null || product.BaseRetailPrice <= query.MaxPrice)
            && (query.CropSeasonId == null || product.HarvestBatch
                .Any(batch => batch.CropSeasonId == query.CropSeasonId)));
        DateOnly today = Today();
        if (!string.IsNullOrWhiteSpace(query.Certification))
        {
            products = products.Where(product => product.FarmerProfile.FarmCertificate
                .Any(certificate => certificate.CertificateType == query.Certification
                    && certificate.VerificationStatus == "Approved"
                    && certificate.IssueDate <= today
                    && (certificate.ExpiryDate == null || certificate.ExpiryDate >= today)));
        }

        if (query.MinRating != null)
        {
            var reviews = PublicReviews();
            products = products.Where(product => reviews
                .Where(review => review.OrderItem.ProductId == product.ProductId)
                .Average(review => (decimal?)review.RatingStars) >= query.MinRating);
        }

        int total = await products.CountAsync(cancellationToken);
        var eligibleReviews = PublicReviews();
        var ordered = query.SortBy switch
        {
            "price-asc" => products.OrderBy(product => product.BaseRetailPrice),
            "price-desc" => products.OrderByDescending(product => product.BaseRetailPrice),
            "rating" => products.OrderByDescending(product => eligibleReviews
                .Where(review => review.OrderItem.ProductId == product.ProductId)
                .Average(review => (decimal?)review.RatingStars)),
            _ => products.OrderByDescending(product => product.CreatedAt)
        };
        var page = ordered.ThenBy(product => product.ProductId)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize);
        return new PagedResponse<ProductSummary>(
            await Summaries(page).ToListAsync(cancellationToken),
            total, query.Page, query.PageSize);
    }

    public async Task<ProductDetail?> FindAsync(long id, CancellationToken cancellationToken)
    {
        var visible = PublicProducts().Where(product => product.ProductId == id);
        ProductSummary? summary = await Summaries(visible).SingleOrDefaultAsync(cancellationToken);
        if (summary == null)
        {
            return null;
        }

        var product = await visible.Include(row => row.ProductImage)
            .Include(row => row.FarmerProfile).ThenInclude(farm => farm.FarmCertificate)
            .Include(row => row.HarvestBatch).ThenInclude(batch => batch.CropSeason)
            .Include(row => row.HarvestBatch).ThenInclude(batch => batch.TraceabilityQRCode)
            .AsSplitQuery().SingleAsync(cancellationToken);
        DateOnly today = Today();
        var certificates = product.FarmerProfile.FarmCertificate
            .Where(certificate => certificate.VerificationStatus == "Approved"
                && certificate.IssueDate <= today
                && (certificate.ExpiryDate == null || certificate.ExpiryDate >= today))
            .Select(certificate => new CertificateResponse(certificate.CertificateType,
                certificate.CertificateNumber, certificate.IssuedBy, certificate.ExpiryDate))
            .ToArray();
        var batches = product.HarvestBatch.Where(batch => batch.Status == "Available"
            && batch.AvailableQuantity > 0
            && (batch.ExpirationDate == null || batch.ExpirationDate >= today))
            .OrderBy(batch => batch.ExpirationDate).ThenBy(batch => batch.HarvestBatchId)
            .Select(batch => new BatchResponse(batch.HarvestBatchId, batch.BatchCode,
                batch.CropSeason.SeasonName, batch.AvailableQuantity, batch.ActualHarvestDate,
                batch.ExpirationDate, batch.TraceabilityQRCode is { IsRevoked: false } qr
                    ? qr.TargetUrl : null)).ToArray();
        // shortcut: latest 100 eligible reviews, add review pagination when volume requires it.
        var reviews = await PublicReviews().Where(review => review.OrderItem.ProductId == id)
            .OrderByDescending(review => review.ReviewDate)
            .ThenByDescending(review => review.ProductReviewId).Take(100)
            .Select(review => new ReviewResponse(review.RatingStars, review.Comment,
                new DateTimeOffset(DateTime.SpecifyKind(review.ReviewDate, DateTimeKind.Utc))))
            .ToListAsync(cancellationToken);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        string? role = await db.User.Where(user => user.UserId == currentUser.UserId
            && user.Status == "Active"
            && (user.LockedUntil == null || user.LockedUntil <= now)).Select(user => user.Role)
            .SingleOrDefaultAsync(cancellationToken);
        return new ProductDetail(summary, product.Description,
            product.FarmerProfile.VerificationStatus, product.FarmerProfile.ReputationScore,
            product.WholesalePrice, product.WholesaleMinQuantity,
            product.ProductImage.OrderByDescending(image => image.IsPrimary)
                .ThenBy(image => image.DisplayOrder).ThenBy(image => image.ProductImageId)
                .Select(image => new ProductImageResponse(image.ImageUrl, image.AltText))
                .ToArray(),
            certificates, batches, reviews,
            role == "RetailCustomer" && summary.AvailableQuantity > 0,
            role == "WholesaleBuyer" && product.WholesalePrice != null
                && await db.WholesaleBuyerProfile.AnyAsync(buyer =>
                    buyer.UserId == currentUser.UserId && buyer.VerificationStatus == "Approved",
                    cancellationToken));
    }

    public async Task<MarketplaceFilters> GetFiltersAsync(CancellationToken cancellationToken)
    {
        var products = PublicProducts();
        DateOnly today = Today();
        var categories = await products.GroupBy(product => new
            { product.CategoryId, product.Category.CategoryName })
            .Select(group => new MarketplaceOption(group.Key.CategoryId,
                group.Key.CategoryName, group.Count())).ToListAsync(cancellationToken);
        var suppliers = await products.GroupBy(product => new
            { product.FarmerProfileId, product.FarmerProfile.FarmName })
            .Select(group => new MarketplaceOption(group.Key.FarmerProfileId,
                group.Key.FarmName, group.Count())).ToListAsync(cancellationToken);
        var seasons = await db.CropSeason.AsNoTracking().Where(season => season.HarvestBatch
            .Any(batch => products.Any(product => product.ProductId == batch.ProductId)))
            .Select(season => new MarketplaceOption(season.CropSeasonId, season.SeasonName, 0))
            .ToListAsync(cancellationToken);
        var regions = await products.Where(product => product.OriginProvince != null)
            .Select(product => product.OriginProvince ?? "").Distinct().OrderBy(region => region)
            .ToListAsync(cancellationToken);
        var certifications = await db.FarmCertificate.AsNoTracking()
            .Where(certificate => certificate.VerificationStatus == "Approved"
                && certificate.IssueDate <= today
                && (certificate.ExpiryDate == null || certificate.ExpiryDate >= today)
                && products.Any(product => product.FarmerProfileId == certificate.FarmerProfileId))
            .Select(certificate => certificate.CertificateType).Distinct().OrderBy(type => type)
            .ToListAsync(cancellationToken);
        return new MarketplaceFilters(categories.OrderBy(category => category.Id).ToArray(),
            suppliers, seasons, regions, certifications);
    }

    private DateOnly Today() => DateOnly.FromDateTime(
        clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).DateTime);

    private IQueryable<Product> PublicProducts()
    {
        DateTime now = clock.GetUtcNow().UtcDateTime;
        return db.Product.AsNoTracking().Where(product => product.ApprovalStatus == "Approved"
            && product.IsPublished && product.Category.IsActive
            && product.FarmerProfile.VerificationStatus == "Approved"
            && product.FarmerProfile.User.Status == "Active"
            && (product.FarmerProfile.User.LockedUntil == null
                || product.FarmerProfile.User.LockedUntil <= now));
    }

    private IQueryable<ProductReview> PublicReviews() => db.ProductReview.AsNoTracking()
        .Where(review => review.IsPurchasedVerified && review.ModerationStatus == "Approved"
            && review.OrderItem.SellerOrder.Order.OrderStatus == "Completed"
            && review.UserId == review.OrderItem.SellerOrder.Order.BuyerUserId);

    private IQueryable<ProductSummary> Summaries(IQueryable<Product> products)
    {
        DateOnly today = Today();
        var reviews = PublicReviews();
        return products.Select(product => new ProductSummary(
            product.ProductId, product.ProductName, product.CategoryId,
            product.Category.CategoryName, product.FarmerProfileId,
            product.FarmerProfile.FarmName, product.OriginProvince,
            product.BaseRetailPrice, product.Unit,
            product.ProductImage.OrderByDescending(image => image.IsPrimary)
                .ThenBy(image => image.DisplayOrder).ThenBy(image => image.ProductImageId)
                .Select(image => image.ImageUrl).FirstOrDefault(),
            product.HarvestBatch.Where(batch => batch.Status == "Available"
                && (batch.ExpirationDate == null || batch.ExpirationDate >= today))
                .Sum(batch => batch.AvailableQuantity),
            reviews.Where(review => review.OrderItem.ProductId == product.ProductId)
                .Average(review => (decimal?)review.RatingStars),
            reviews.Count(review => review.OrderItem.ProductId == product.ProductId),
            product.FarmerProfile.FarmCertificate.Where(certificate =>
                certificate.VerificationStatus == "Approved" && certificate.IssueDate <= today
                && (certificate.ExpiryDate == null || certificate.ExpiryDate >= today))
                .OrderBy(certificate => certificate.CertificateType)
                .Select(certificate => certificate.CertificateType).Distinct().ToList()));
    }
}
