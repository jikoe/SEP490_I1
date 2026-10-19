using AgriSustain.Application.DTOs.Authentication;

namespace AgriSustain.Application.Interfaces.Identity;

public interface IAccountManagementService
{
    Task UpdateAsync(long accountId, UpdateAccountRequestDto request, CancellationToken cancellationToken = default);
}
