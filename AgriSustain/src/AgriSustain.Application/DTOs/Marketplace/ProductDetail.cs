namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record ProductDetail(
    ProductSummary Product,
    string? Description,
    string FarmVerification,
    decimal FarmRating,
    decimal? WholesalePrice,
    decimal? WholesaleMinQuantity,
    IReadOnlyList<ProductImageResponse> Images,
    IReadOnlyList<CertificateResponse> Certificates,
    IReadOnlyList<BatchResponse> Batches,
    IReadOnlyList<ReviewResponse> Reviews,
    bool CanAddToCart,
    bool CanStartB2B);
