using AgriSustain.Domain.Entities.Accounts;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<Account?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddAsync(Account account, CancellationToken cancellationToken = default);
    Task UpdateAsync(Account account, CancellationToken cancellationToken = default);
}
