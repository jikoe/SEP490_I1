using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Marketplace;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface ICartRepository
{
    /// <summary>
    /// Locks the owned cart and applies the use case decision without reserving stock.
    /// </summary>
    Task<Result<decimal>> UpdateAsync(
        long userId, long productId, Func<CartContext, Result<decimal>> update,
        CancellationToken cancellationToken);
}
