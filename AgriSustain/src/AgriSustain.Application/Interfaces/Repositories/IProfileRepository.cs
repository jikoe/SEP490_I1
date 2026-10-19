using AgriSustain.Application.DTOs.Profiles;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface IProfileRepository
{
    Task<UserProfileResponse?> GetProfileAsync(long userId, CancellationToken cancellationToken);

    Task UpdateConsumerProfileAsync(
        long userId, UpdateConsumerProfileRequest request, CancellationToken cancellationToken);

    Task UpdateFarmerProfileAsync(
        long userId, UpdateFarmerProfileRequest request, bool requiresReverification, CancellationToken cancellationToken);

    Task SubmitFarmerKycAsync(
        long userId, string? notes, CancellationToken cancellationToken);
}
