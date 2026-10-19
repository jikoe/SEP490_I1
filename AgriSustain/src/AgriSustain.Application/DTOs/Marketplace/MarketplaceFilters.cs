namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record MarketplaceFilters(
    IReadOnlyList<MarketplaceOption> Categories,
    IReadOnlyList<MarketplaceOption> Suppliers,
    IReadOnlyList<MarketplaceOption> Seasons,
    IReadOnlyList<string> Regions,
    IReadOnlyList<string> Certifications);
