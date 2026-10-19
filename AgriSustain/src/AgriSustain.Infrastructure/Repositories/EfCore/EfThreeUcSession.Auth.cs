using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Domain.Entities.Accounts;
using Db = AgriSustain.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

internal sealed partial class EfThreeUcSession
{
    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken)
    {
        return await _db.User.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken)
    {
        return await _db.User.AnyAsync(u => u.PhoneNumber == phone, cancellationToken);
    }

    public Task SavePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken)
    {
        _dependencies.PendingRegistrations[registration.Email] = registration;
        return Task.CompletedTask;
    }

    public Task<PendingRegistration?> FindPendingRegistrationAsync(string email, CancellationToken cancellationToken)
    {
        _dependencies.PendingRegistrations.TryGetValue(email, out var registration);
        return Task.FromResult<PendingRegistration?>(registration);
    }

    public Task UpdatePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken)
    {
        _dependencies.PendingRegistrations[registration.Email] = registration;
        return Task.CompletedTask;
    }

    public Task RemovePendingRegistrationAsync(string email, CancellationToken cancellationToken)
    {
        _dependencies.PendingRegistrations.TryRemove(email, out _);
        return Task.CompletedTask;
    }

    public async Task<UserAccount> CreateUserAsync(
        string fullName, string phoneNumber, string email, string passwordHash,
        string role, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        var dbUser = new Db.User
        {
            Username = email,
            FullName = fullName,
            PhoneNumber = phoneNumber,
            Email = email,
            PasswordHash = passwordHash,
            Role = role,
            Status = "Active",
            IsEmailVerified = true,
            CreatedAt = createdAt.UtcDateTime,
            FailedLoginCount = 0
        };

        _db.User.Add(dbUser);
        await _db.SaveChangesAsync(cancellationToken);

        return new UserAccount
        {
            Id = dbUser.UserId,
            Name = dbUser.FullName,
            Email = dbUser.Email,
            Phone = dbUser.PhoneNumber,
            Role = dbUser.Role,
            OrganizationName = null,
            VerificationStatus = null,
            CreatedAt = Utc(dbUser.CreatedAt)
        };
    }

    public async Task CreateFarmerProfileAsync(
        long userId, string farmName, string representativeName, string address,
        string provinceCode, string verificationStatus, decimal reputationScore,
        CancellationToken cancellationToken)
    {
        var profile = new Db.FarmerProfile
        {
            UserId = userId,
            FarmName = farmName,
            RepresentativeName = representativeName,
            Address = address,
            ProvinceCode = provinceCode,
            VerificationStatus = verificationStatus,
            ReputationScore = reputationScore
        };

        _db.FarmerProfile.Add(profile);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
