using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Domain.ValueObjects;
using AgriSustain.Infrastructure.Data.Demo;
using Xunit;

namespace AgriSustain.IntegrationTests;

public sealed class OrderTrackingIntegrationTests
{
    private static void SeedCustomerOrders(DemoState state)
    {
        var address = new AddressSnapshot(
            "Khách mua demo", "0901234567", "Thành phố Hà Nội",
            "Phường Dịch Vọng Hậu", "Số 18, Phố Duy Tân", "01", "001");

        var seller1 = new SellerOrderSnapshot(20, "SO-201", 150000, 0, 30000, 180000);
        var line1 = new OrderLineSnapshot(
            1, 101, 20, "HTX Rau Xanh", "Cải ngọt hữu cơ", "kg", 5, 30000, 150000,
            [new BatchAllocationSnapshot(201, "LOT-RAU-01", 5)]);

        var order1 = new RetailOrder
        {
            Id = 101,
            BuyerId = 10,
            Code = "ORD-2026-9812",
            QuoteId = Guid.NewGuid(),
            RequestKey = Guid.NewGuid(),
            RequestFingerprint = "fingerprint1",
            Address = address,
            DeliveryMethod = "Giao hàng tiêu chuẩn",
            PaymentMethod = "VNPay QR",
            CreatedAt = DateTimeOffset.Parse("2026-10-18T09:30:00Z"),
            ReservationExpiresAt = DateTimeOffset.Parse("2026-10-18T10:30:00Z"),
            Lines = [line1],
            Sellers = [seller1],
            DeliveryFee = 30000,
            VoucherDiscount = 0,
            PointsDiscount = 0,
            RedeemedPoints = 0
        };

        var seller2 = new SellerOrderSnapshot(21, "SO-202", 200000, 0, 30000, 230000);
        var line2 = new OrderLineSnapshot(
            2, 102, 21, "Nông trại Mùa Vàng", "Cam sành", "kg", 5, 40000, 200000,
            [new BatchAllocationSnapshot(202, "LOT-CAM-01", 5)]);

        var order2 = new RetailOrder
        {
            Id = 102,
            BuyerId = 10,
            Code = "ORD-2026-8940",
            QuoteId = Guid.NewGuid(),
            RequestKey = Guid.NewGuid(),
            RequestFingerprint = "fingerprint2",
            Address = address,
            DeliveryMethod = "Giao hàng tiêu chuẩn",
            PaymentMethod = "VNPay QR",
            CreatedAt = DateTimeOffset.Parse("2026-10-12T14:15:00Z"),
            ReservationExpiresAt = DateTimeOffset.Parse("2026-10-12T15:15:00Z"),
            Lines = [line2],
            Sellers = [seller2],
            DeliveryFee = 30000,
            VoucherDiscount = 0,
            PointsDiscount = 0,
            RedeemedPoints = 0
        };

        state.Orders[101] = order1;
        state.Orders[102] = order2;
    }

    [Fact]
    public async Task GetOrders_RetailCustomer_ReturnsPaginatedHistorySortedDesc()
    {
        // Corresponds to IT-RC04-01
        using var factory = new ApiFactory(configureSeed: SeedCustomerOrders);
        using var client = factory.Client("retail-customer");

        var response = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<OrderSummaryDto>>();
        Assert.NotNull(paged);
        Assert.Equal(2, paged.Total);
        Assert.Equal("ORD-2026-9812", paged.Items[0].OrderCode); // Newest first
    }

    [Fact]
    public async Task GetOrders_FilterByKeyword_ReturnsMatchingOnly()
    {
        // Corresponds to IT-RC04-02
        using var factory = new ApiFactory(configureSeed: SeedCustomerOrders);
        using var client = factory.Client("retail-customer");

        var response = await client.GetAsync("/api/orders?keyword=8940");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<OrderSummaryDto>>();
        Assert.NotNull(paged);
        Assert.Single(paged.Items);
        Assert.Equal("ORD-2026-8940", paged.Items[0].OrderCode);
    }

    [Fact]
    public async Task GetOrders_CustomerWithNoOrders_ReturnsEmptyList()
    {
        // Corresponds to IT-RC04-03
        using var factory = new ApiFactory(); // user 11 has no orders
        using var client = factory.Client("other-retail-customer");

        var response = await client.GetAsync("/api/orders");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var paged = await response.Content.ReadFromJsonAsync<PagedResponse<OrderSummaryDto>>();
        Assert.NotNull(paged);
        Assert.Empty(paged.Items);
        Assert.Equal(0, paged.Total);
    }

    [Fact]
    public async Task GetOrderDetail_ValidOrderId_ReturnsDetailsWithBatchAndPayment()
    {
        // Corresponds to IT-RC04-04
        using var factory = new ApiFactory(configureSeed: SeedCustomerOrders);
        using var client = factory.Client("retail-customer");

        var response = await client.GetAsync("/api/orders/101");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<OrderDetailDto>();
        Assert.NotNull(detail);
        Assert.Equal("ORD-2026-9812", detail.OrderCode);
        Assert.NotEmpty(detail.SellerOrders);
        var seller = detail.SellerOrders[0];
        Assert.NotEmpty(seller.Items);
        var item = seller.Items[0];
        Assert.Equal("Cải ngọt hữu cơ", item.ProductName);
        Assert.NotNull(item.HarvestBatchCode);
    }

    [Fact]
    public async Task GetOrderDetail_OtherCustomerOrder_ReturnsNotFound()
    {
        // Verify privacy: other customer cannot view buyer 10's order
        using var factory = new ApiFactory(configureSeed: SeedCustomerOrders);
        using var client = factory.Client("other-retail-customer"); // User 11

        var response = await client.GetAsync("/api/orders/101");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
