using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Marketplace;

namespace AgriSustain.Application.Interfaces.Services;

public interface ICartService
{
    Task<Result<decimal>> AddAsync(
        AddCartItemRequest request, CancellationToken cancellationToken);
}
