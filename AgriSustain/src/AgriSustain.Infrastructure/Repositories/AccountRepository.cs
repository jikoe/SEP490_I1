using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Domain.Entities.Accounts;
using AgriSustain.Infrastructure.Data.Contexts;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories;

public class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _dbContext;

    public AccountRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Account?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
    }

    public async Task<Account?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Users
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(account, cancellationToken);
    }

    public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        _dbContext.Users.Update(account);
        return Task.CompletedTask;
    }

    public Task<UserAccount?> FindAsync(long id, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task<PagedResponse<UserResponse>> SearchAsync(
        UserQuery query, DateTimeOffset now, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }

    public Task SaveAsync(UserAccount account, CancellationToken cancellationToken)
    {
        throw new NotSupportedException();
    }
}
