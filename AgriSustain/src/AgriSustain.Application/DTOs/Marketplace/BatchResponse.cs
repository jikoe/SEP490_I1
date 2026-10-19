namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record BatchResponse(
    long Id, string Code, string Season, decimal AvailableQuantity,
    DateOnly? HarvestedOn, DateOnly? ExpiresOn, string? TraceabilityUrl);
