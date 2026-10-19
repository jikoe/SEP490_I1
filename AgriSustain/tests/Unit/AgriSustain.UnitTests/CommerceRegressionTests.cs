using AgriSustain.Application.DTOs.Commerce;
using AgriSustain.Application.Services.Commerce;
using Xunit;

namespace AgriSustain.UnitTests;
public sealed class CommerceRegressionTests
{
    [Fact]
    public async Task Checkout_review_survives_SQL_decimal_scale_roundtrip()
    {
        var repo = new CommerceDouble(); repo.Retail(repo.Line());
        var service = new CartService(repo, new CommerceUser(2), new CommerceTestClock());
        var shown = (await service.GetAsync()).Value!;
        repo.Lines[0].Quantity = 2.000m;
        repo.Lines[0].Offer!.RetailPrice = 30000.00m;
        repo.Lines[0].Offer!.WholesaleMinimum = 10.000m;
        repo.Lines[0].Offer!.QuantityStep = 1.000m;
        Assert.True((await service.PrepareCheckoutAsync(shown.ReviewToken)).Value!.CanProceedToCheckout);
    }
    [Fact]
    public async Task Successful_new_listing_retry_reuses_durable_request_key()
    {
        var repo = new CommerceDouble(); var service = new ProductService(repo, new CommerceUser(1));
        var input = CommerceDesignTests.Input();
        var first = await service.SubmitAsync(input); var repeated = await service.SubmitAsync(input);
        Assert.True(first.IsSuccess); Assert.Equal(first.Value!.Id, repeated.Value!.Id);
        Assert.Equal(1, repo.ListingSaves);
    }
    [Fact]
    public async Task Price_only_update_does_not_reset_approval_but_stale_revision_is_rejected()
    {
        var repo = new CommerceDouble(); var service = new ProductService(repo, new CommerceUser(1));
        var input = CommerceDesignTests.Input() with {RetailPrice = 31000, WholesaleMinimum = 10, WholesalePrice = 25000,
            Images = [new("", "", null, true, "/image/1")]};
        Assert.True((await service.UpdateAsync(1, input)).IsSuccess);
        Assert.Equal("MSG122", (await service.UpdateAsync(1, input)).Error!.Code);
        Assert.True(repo.Product.Published); Assert.Equal("Approved", repo.Product.Approval);
    }
    [Fact]
    public async Task Receive_then_accept_keeps_original_delivery_time()
    {
        var repo = new CommerceDouble(); repo.Retail(); repo.Order.Status = "Shipping";
        var clock = new CommerceTestClock(); var service = new OrderFulfillmentService(repo, new CommerceUser(2), clock);
        Assert.True((await service.ReceiveAsync(100)).IsSuccess); var original = repo.Order.DeliveredAt;
        clock.Now = clock.Now.AddHours(3); Assert.True((await service.ReceiveAsync(100, true)).IsSuccess);
        Assert.Equal(original, repo.Order.DeliveredAt); Assert.True(repo.Order.ReceivedAt > original);
    }
}
