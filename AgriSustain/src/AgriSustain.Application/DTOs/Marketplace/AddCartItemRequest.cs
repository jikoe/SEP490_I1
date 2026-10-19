namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record AddCartItemRequest(long ProductId, decimal Quantity);
