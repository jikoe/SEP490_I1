using System.Data;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

public sealed class CartRepository(
    AgriSustainDbContext db,
    IMarketplaceRepository marketplace,
    TimeProvider clock) : ICartRepository
{
    public async Task<Result<decimal>> UpdateAsync(
        long userId, long productId, Func<CartContext, Result<decimal>> update,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        bool eligible = await db.User.AnyAsync(user => user.UserId == userId
            && user.Status == "Active" && user.Role == "RetailCustomer"
            && (user.LockedUntil == null || user.LockedUntil <= now), cancellationToken);
        var detail = await marketplace.FindAsync(productId, cancellationToken);
        Cart? cart = await db.Cart.Include(row => row.CartItem)
            .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken);
        CartItem? item = cart?.CartItem.SingleOrDefault(row =>
            row.ProductId == productId && row.PreOrderCampaignId == null);
        var outcome = update(new CartContext(eligible, detail?.Product, item?.Quantity ?? 0));
        if (!outcome.IsSuccess)
        {
            return outcome;
        }

        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            db.Cart.Add(cart);
        }

        if (item == null)
        {
            cart.CartItem.Add(new CartItem
            {
                ProductId = productId, Quantity = outcome.Value, IsSelected = true, AddedAt = now
            });
        }
        else
        {
            item.Quantity = outcome.Value;
            item.IsSelected = true;
        }

        cart.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return outcome;
    }
}
