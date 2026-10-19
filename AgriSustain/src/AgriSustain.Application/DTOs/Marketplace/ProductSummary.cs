namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record ProductSummary(
    long Id,
    string Name,
    long CategoryId,
    string CategoryName,
    long SupplierId,
    string SupplierName,
    string? Region,
    decimal Price,
    string Unit,
    string? ImageUrl,
    decimal AvailableQuantity,
    decimal? Rating,
    int ReviewCount,
    IReadOnlyList<string> Certifications);
