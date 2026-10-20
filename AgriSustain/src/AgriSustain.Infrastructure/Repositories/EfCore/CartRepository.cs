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
        await db.Database.ExecuteSqlRawAsync("""
            DECLARE @Result int;
            EXEC @Result=sys.sp_getapplock @Resource='AgriSustain.ThreeUc.SqlDemo',
                @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
            IF @Result<0 THROW 51140,'Commerce transaction conflict',1;
            """, cancellationToken);
        DateTime now = clock.GetUtcNow().UtcDateTime;
        bool eligible = await db.User.AnyAsync(user => user.UserId == userId
            && user.Status == "Active" && user.Role == "RetailCustomer"
            && (user.LockedUntil == null || user.LockedUntil <= now), cancellationToken);
        var detail = await marketplace.FindAsync(productId, cancellationToken);
        Cart? cart = await db.Cart.Include(row => row.CartItem)
            .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken);
        var items = cart?.CartItem.Where(row => row.ProductId == productId
            && row.PreOrderCampaignId == null).ToArray() ?? [];
        var outcome = update(new CartContext(eligible, detail?.Product, items.Sum(x => x.Quantity)));
        if (!outcome.IsSuccess)
        {
            return outcome;
        }

        decimal step = await db.Product.Where(p => p.ProductId == productId)
            .Select(p => p.QuantityStep).SingleAsync(cancellationToken);
        if (!AgriSustain.Application.Services.RetailCustomer.CartService.QuantityValid(outcome.Value, step))
            return Result<decimal>.Failure(new Error("MSG167", "Số lượng phải đúng bước bán của sản phẩm."));

        var today = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date);
        var batches = await db.HarvestBatch.Where(b => b.ProductId == productId
            && b.ActualHarvestDate != null && b.StockEnteredAt != null
            && (b.Status == "Available" || b.Status == "InStock" || b.Status == "FullyReserved")
            && (b.ExpirationDate == null || b.ExpirationDate > today) && b.AvailableQuantity > 0)
            .OrderBy(b => b.ExpirationDate == null).ThenBy(b => b.ExpirationDate)
            .ThenBy(b => b.HarvestBatchId).ToArrayAsync(cancellationToken);
        var plan = items.Where(i => i.HarvestBatchId.HasValue).GroupBy(i => i.HarvestBatchId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        if (plan.Any(p => !batches.Any(b => b.HarvestBatchId == p.Key && b.AvailableQuantity >= p.Value)))
            return Result<decimal>.Failure(new Error("MSG88", "Lô trong giỏ đã thay đổi. Kiểm tra lại giỏ trước khi thêm."));
        decimal remaining = outcome.Value - plan.Values.Sum();
        foreach (var batch in batches)
        {
            decimal prior = plan.GetValueOrDefault(batch.HarvestBatchId);
            decimal extra = Math.Min(remaining, decimal.Floor((batch.AvailableQuantity - prior) / step) * step);
            if (extra > 0) { plan[batch.HarvestBatchId] = prior + extra; remaining -= extra; }
            if (remaining == 0) break;
        }
        if (remaining != 0)
            return Result<decimal>.Failure(new Error("MSG38", "Số lượng trong giỏ vượt quá tồn kho khả dụng.", "Conflict"));
        if ((cart?.CartItem.Count ?? 0) - items.Length + plan.Count > 100)
            return Result<decimal>.Failure(new Error("MSG120", "Tối đa 100 dòng trong giỏ."));

        if (cart == null)
        {
            cart = new Cart { UserId = userId };
            db.Cart.Add(cart);
        }

        // Preserve previously selected batch identities, and place only the added/legacy
        // quantity into FEFO batches. This does not reserve or decrement inventory.
        var retained = new HashSet<CartItem>();
        foreach (var line in plan)
        {
            var item = items.FirstOrDefault(i => i.HarvestBatchId == line.Key);
            if (item == null)
            {
                item = new CartItem { ProductId = productId, HarvestBatchId = line.Key, AddedAt = now };
                cart.CartItem.Add(item);
            }
            item.Quantity = line.Value;
            item.IsSelected = true;
            retained.Add(item);
        }
        db.CartItem.RemoveRange(items.Where(i => !retained.Contains(i)));

        cart.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return outcome;
    }
}
