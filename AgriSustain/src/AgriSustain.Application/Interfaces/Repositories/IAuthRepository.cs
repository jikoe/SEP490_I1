using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Domain.Entities.Accounts;

namespace AgriSustain.Application.Interfaces.Repositories;

public sealed record PendingRegistration(
    string FullName,
    string PhoneNumber,
    string Email,
    string PasswordHash,
    string Role,
    string OtpCode,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    int FailedAttempts);

public interface IAuthRepository
{
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken);
    Task SavePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken);
    Task<PendingRegistration?> FindPendingRegistrationAsync(string email, CancellationToken cancellationToken);
    Task UpdatePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken);
    Task RemovePendingRegistrationAsync(string email, CancellationToken cancellationToken);
    Task<UserAccount> CreateUserAsync(
        string fullName, string phoneNumber, string email, string passwordHash,
        string role, DateTimeOffset createdAt, CancellationToken cancellationToken);
    Task CreateFarmerProfileAsync(
        long userId, string farmName, string representativeName, string address,
        string provinceCode, string verificationStatus, decimal reputationScore,
        CancellationToken cancellationToken);
}
