namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record CartContext(
    bool IsEligible, ProductSummary? Product, decimal CurrentQuantity);
