namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record MarketplaceQuery(
    string? Keyword = null,
    long? CategoryId = null,
    long? SupplierId = null,
    string? Region = null,
    long? CropSeasonId = null,
    string? Certification = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    decimal? MinRating = null,
    string SortBy = "newest",
    int Page = 1,
    int PageSize = 12);
