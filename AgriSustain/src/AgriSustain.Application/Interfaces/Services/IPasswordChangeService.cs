using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;

namespace AgriSustain.Application.Interfaces.Services;

public interface IPasswordChangeService
{
    Task<Result<string>> ChangeAsync(
        ChangePasswordRequest request, CancellationToken cancellationToken);
}
