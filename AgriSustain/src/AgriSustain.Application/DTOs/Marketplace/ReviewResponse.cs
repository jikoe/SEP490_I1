namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record ReviewResponse(byte Rating, string? Comment, DateTimeOffset CreatedAt);
