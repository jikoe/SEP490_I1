using AgriSustain.Application.DTOs.Authentication;

namespace AgriSustain.Application.Interfaces.Identity;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<LoginResponseDto> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}
