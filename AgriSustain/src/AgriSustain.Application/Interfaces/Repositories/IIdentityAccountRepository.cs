using AgriSustain.Domain.Entities.Accounts;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface IIdentityAccountRepository
{
    Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken);
    Task<Account?> GetByIdAsync(long id, CancellationToken cancellationToken);
    Task UpdateAsync(Account account, CancellationToken cancellationToken);
}
