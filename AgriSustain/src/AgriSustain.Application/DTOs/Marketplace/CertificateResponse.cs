namespace AgriSustain.Application.DTOs.Marketplace;

public sealed record CertificateResponse(
    string Type, string Number, string IssuedBy, DateOnly? ExpiresOn);
