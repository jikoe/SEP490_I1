using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Services.Shared;
using AgriSustain.Application.Services.Farmer;
using AgriSustain.Application.Services.RetailCustomer;
using AgriSustain.Infrastructure.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using D = AgriSustain.Infrastructure.Data.Entities;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

internal sealed partial class FarmerRetailRepository
{
    public async Task<BasketLine[]> CartAsync(long userId)
    {
        var rows = await db.CartItem.Where(x => x.Cart.UserId == userId && x.PreOrderCampaignId == null).OrderBy(x => x.CartItemId).ToArrayAsync(ct);
        var offers = new Dictionary<long, Listing?>();
        foreach (long id in rows.Select(x => x.ProductId).Distinct()) offers[id] = await ListingAsync(id);
        return rows.Select(x => new BasketLine {Id = x.CartItemId, ProductId = x.ProductId, BatchId = x.HarvestBatchId,
            Quantity = x.Quantity, Selected = x.IsSelected, Offer = offers[x.ProductId]}).ToArray();
    }
    public async Task SaveCartAsync(long userId, BasketLine[] lines)
    {
        var cart = await db.Cart.Include(x => x.CartItem).SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (cart is null && lines.Length == 0) return;
        if (cart is null) { cart = new D.Cart {UserId = userId}; db.Cart.Add(cart); }
        var prior = cart.CartItem.Where(x => x.PreOrderCampaignId == null).ToArray();
        db.CartItem.RemoveRange(prior.Where(x => !lines.Any(l => l.Id == x.CartItemId)));
        foreach (var line in lines)
        {
            var row = line.Id == 0 ? new D.CartItem { Cart = cart, ProductId = line.ProductId, AddedAt = Now } : prior.Single(x => x.CartItemId == line.Id);
            if (line.Id == 0) db.CartItem.Add(row);
            row.HarvestBatchId = line.BatchId; row.Quantity = line.Quantity; row.IsSelected = line.Selected;
        }
        cart.UpdatedAt = Now;
        await db.SaveChangesAsync(ct);
        foreach (var line in lines.Where(x => x.Id == 0)) line.Id = cart.CartItem.Single(x => x.ProductId == line.ProductId && x.HarvestBatchId == line.BatchId && x.PreOrderCampaignId == null).CartItemId;
    }
}
