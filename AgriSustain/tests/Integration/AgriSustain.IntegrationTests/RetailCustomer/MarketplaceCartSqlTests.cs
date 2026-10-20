using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using AgriSustain.Application.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriSustain.IntegrationTests;

public sealed class MarketplaceCartSqlTests
{
    [SqlFact]
    public async Task Marketplace_add_preserves_selected_batches_and_cart_can_read_split_lines()
    {
        await using var fixture = await FarmerRetailSqlFixture.Create();
        await fixture.Ok<CartResponse>(await fixture.Buyer.PostAsJsonAsync("/api/cart/items",
            new AddCartItem(fixture.ProductId, fixture.Batch2Id, 3)));
        var initial = await fixture.Buyer.GetFromJsonAsync<CartResponse>("/api/cart");
        long originalLine = Assert.Single(initial!.Items).Id;

        using (var scope = fixture.Host.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ICartRepository>();
            var result = await repository.UpdateAsync(fixture.BuyerId, fixture.ProductId,
                cart => Result<decimal>.Success(cart.CurrentQuantity + 22), default);
            Assert.True(result.IsSuccess);
            Assert.Equal(25, result.Value);
        }
        var cart = await fixture.Buyer.GetFromJsonAsync<CartResponse>("/api/cart");
        Assert.Equal(2, cart!.Items.Length);
        Assert.All(cart.Items, line => Assert.True(line.Available));
        Assert.Equal(25, cart.Items.Sum(line => line.Quantity));
        Assert.Contains(cart.Items, line => line.Id == originalLine
            && line.BatchId == fixture.Batch2Id && line.Quantity == 5);
        Assert.Equal(0, await fixture.Number("SELECT SUM(ReservedQuantity) FROM dbo.HarvestBatch"));

        using (var scope = fixture.Host.Services.CreateScope())
        {
            var repository = scope.ServiceProvider.GetRequiredService<ICartRepository>();
            Assert.True((await repository.UpdateAsync(fixture.BuyerId, fixture.ProductId,
                current => Result<decimal>.Success(current.CurrentQuantity + 1), default)).IsSuccess);
        }
        cart = await fixture.Buyer.GetFromJsonAsync<CartResponse>("/api/cart");
        Assert.Equal(26, cart!.Items.Sum(line => line.Quantity));
        Assert.Equal(2, cart.Items.Length);
    }
}
