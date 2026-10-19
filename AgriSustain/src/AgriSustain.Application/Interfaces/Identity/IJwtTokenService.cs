using AgriSustain.Domain.Entities.Accounts;

namespace AgriSustain.Application.Interfaces.Identity;

public interface IJwtTokenService
{
    string CreateAccessToken(Account account);
    string CreateRefreshToken(Account account);
    long ValidateRefreshToken(string token);
}
