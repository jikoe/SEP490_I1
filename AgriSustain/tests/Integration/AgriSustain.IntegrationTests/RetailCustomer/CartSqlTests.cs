using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using Xunit;

namespace AgriSustain.IntegrationTests;

// Real SQL/API integration coverage; never used by the fake-only unit suite.
public sealed class CartSqlTests
{
    public static IEnumerable<object[]> CartCases => Enumerable.Range(1,21).Select(n => new object[] {$"IT-RC-01-{n:000}",n});
    [SqlTheory, MemberData(nameof(CartCases))]
    public async Task Cart_SQL_design(string caseId,int n)
    {
        await using var f=await FarmerRetailSqlFixture.Create(); Assert.StartsWith("IT-RC-01",caseId);
        CartResponse cart;
        if(n==10) { cart=await f.Cart(); Assert.Empty(cart.Items); Assert.Equal("MSG168",cart.MessageCode); return; }
        cart=await f.Add(n is 5 or 17?9:n==13?5:n==11?3:2);
        long id=cart.Items[0].Id;
        switch(n)
        {
            case 1: Assert.Equal(60000,cart.SelectedSubtotal); Assert.Equal(f.BatchId,cart.Items[0].BatchId); Assert.True(cart.Items[0].Available); break;
            case 2:
                var handover=await f.Ok<CartResponse>(await f.Buyer.PostAsJsonAsync("/api/cart/prepare-checkout",new CheckoutReview(cart.ReviewToken)));
                Assert.True(handover.CanProceedToCheckout); Assert.Equal(60000,handover.SelectedSubtotal); break;
            case 3: cart=await f.Add(3); Assert.Single(cart.Items); Assert.Equal(5,cart.Items[0].Quantity); break;
            case 4: cart=await f.Add(3,f.Batch2Id); Assert.Equal(2,cart.Items.Length); Assert.Equal(2,cart.Items[0].Quantity); break;
            case 5:
                foreach(var qty in new[]{10,11,9}) {cart=await f.Ok<CartResponse>(await f.Buyer.PutAsJsonAsync($"/api/cart/items/{id}",new {Quantity=qty})); Assert.Equal(qty>=10?25000:30000,cart.Items[0].UnitPrice);}
                Assert.Equal(270000,cart.SelectedSubtotal); break;
            case 6:
                await f.Sql($"UPDATE dbo.Product SET WholesalePrice=NULL,WholesaleMinQuantity=NULL WHERE ProductId={f.ProductId}");
                cart=await f.Ok<CartResponse>(await f.Buyer.PutAsJsonAsync($"/api/cart/items/{id}",new {Quantity=10})); Assert.Equal(300000,cart.SelectedSubtotal); break;
            case 7:
            case 8:
            case 9:
                if(n==8) cart=await f.Add(3,f.Batch2Id);
                var ids=cart.Items.Select(x=>x.Id).ToArray();
                cart=await f.Ok<CartResponse>(await f.Buyer.PostAsJsonAsync("/api/cart/remove",new RemoveCartItems(ids,n!=9)));
                if(n==9) Assert.Single(cart.Items); else Assert.Empty(cart.Items); break;
            case 11:
                foreach(var invalid in new[]{"0","-1","1.5","\"text\"","\"\""}) {
                    using var content=new StringContent("{\"quantity\":"+invalid+"}",System.Text.Encoding.UTF8,"application/json");
                    Assert.False((await f.Buyer.PutAsync($"/api/cart/items/{id}",content)).IsSuccessStatusCode);
                }
                Assert.Equal(3,(await f.Cart()).Items[0].Quantity); break;
            case 12:
                await f.Sql($"UPDATE dbo.HarvestBatch SET AvailableQuantity=2.4 WHERE HarvestBatchId={f.BatchId}");
                cart=await f.Ok<CartResponse>(await f.Buyer.PutAsJsonAsync($"/api/cart/items/{id}",new {Quantity=3})); Assert.Equal(2,cart.Items[0].Quantity); Assert.Equal("MSG86",cart.MessageCode); break;
            case 13:
            case 14:
            case 16:
            case 17:
            case 21:
                if(n==13) await f.Sql($"UPDATE dbo.HarvestBatch SET AvailableQuantity=3 WHERE HarvestBatchId={f.BatchId}");
                if(n==14) await f.Sql($"UPDATE dbo.HarvestBatch SET AvailableQuantity=0.5 WHERE HarvestBatchId={f.BatchId}");
                if(n==16) { await f.Add(2,f.Batch2Id); cart=await f.Cart(); await f.Sql($"UPDATE dbo.HarvestBatch SET Status='Recalled' WHERE HarvestBatchId={f.BatchId}"); }
                if(n==17) await f.Sql($"UPDATE dbo.Product SET BaseRetailPrice=32000,WholesaleMinQuantity=20 WHERE ProductId={f.ProductId}");
                if(n==21) await f.Sql($"UPDATE dbo.Category SET IsActive=0 WHERE CategoryId={f.CategoryId}");
                cart=await f.Ok<CartResponse>(await f.Buyer.PostAsJsonAsync("/api/cart/prepare-checkout",new CheckoutReview(cart.ReviewToken)));
                Assert.False(cart.CanProceedToCheckout); Assert.Equal(n==13?"MSG86":n==17?"MSG118":"MSG88",cart.MessageCode);
                if(n==13) Assert.Equal(3,cart.Items[0].Quantity);
                if(n is 13 or 16 or 17) Assert.True((await f.Ok<CartResponse>(await f.Buyer.PostAsJsonAsync("/api/cart/prepare-checkout",new CheckoutReview(cart.ReviewToken)))).CanProceedToCheckout); break;
            case 15:
                cart=await f.Ok<CartResponse>(await f.Buyer.PostAsJsonAsync("/api/cart/selection",new CartSelection([id],false)));
                cart=await f.Ok<CartResponse>(await f.Buyer.PostAsJsonAsync("/api/cart/prepare-checkout",new CheckoutReview(cart.ReviewToken))); Assert.Equal("MSG87",cart.MessageCode); break;
            case 18:
                await f.Ok<CartResponse>(await f.Buyer.PostAsJsonAsync("/api/cart/prepare-checkout",new CheckoutReview(cart.ReviewToken)));
                Assert.Single((await f.Cart()).Items); Assert.True((await f.Cart()).Items[0].Selected); break;
            case 19:
                // Seed the already-placed state owned by UC-RC-02, then verify returning does not restore it.
                await f.SeedOrder(); await f.Sql($"DELETE dbo.CartItem WHERE CartItemId={id}");
                Assert.Empty((await f.Cart()).Items); Assert.Equal(1,await f.Number("SELECT COUNT(*) FROM dbo.[Order]")); return;
            case 20:
                Assert.Equal(HttpStatusCode.Forbidden,(await f.Other.PutAsJsonAsync($"/api/cart/items/{id}",new {Quantity=9})).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden,(await f.Other.PostAsJsonAsync("/api/cart/selection",new CartSelection([id],true))).StatusCode);
                Assert.Equal(HttpStatusCode.Forbidden,(await f.Other.PostAsJsonAsync("/api/cart/remove",new RemoveCartItems([id],true))).StatusCode);
                Assert.Equal(2,(await f.Cart()).Items[0].Quantity); break;
        }
        Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.[Order]"));
        Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.PaymentTransaction"));
        Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.VoucherRedemption"));
        Assert.Equal(0,await f.Number("SELECT SUM(ReservedQuantity) FROM dbo.HarvestBatch"));
    }
}
