using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Domain.Entities.Accounts;

namespace AgriSustain.Infrastructure.Services.Authentication;

public class AccountManagementService : IAccountManagementService
{
    private readonly IAccountRepository _accountRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AccountManagementService(
        IAccountRepository accountRepository,
        IUnitOfWork unitOfWork)
    {
        _accountRepository = accountRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task UpdateAsync(long accountId, UpdateAccountRequestDto request, CancellationToken cancellationToken = default)
    {
        var account = await _accountRepository.GetByIdAsync(accountId, cancellationToken);
        if (account is null)
        {
            throw new KeyNotFoundException($"Account with id '{accountId}' was not found.");
        }

        if (!string.IsNullOrWhiteSpace(request.FullName))
        {
            account.FullName = request.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            account.Email = request.Email.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            account.Role = request.Role.Trim();
        }

        if (request.IsActive.HasValue)
        {
            account.IsActive = request.IsActive.Value;
        }

        await _accountRepository.UpdateAsync(account, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
