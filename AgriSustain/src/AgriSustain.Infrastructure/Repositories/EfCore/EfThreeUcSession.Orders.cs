using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Repositories;
using Db = AgriSustain.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

internal sealed partial class EfThreeUcSession
{
    public async Task<PagedResponse<OrderSummaryDto>> GetOrdersByBuyerAsync(
        long buyerId, OrderQuery query, CancellationToken cancellationToken)
    {
        var ordersQuery = _db.Order
            .AsNoTracking()
            .Where(o => o.BuyerUserId == buyerId);

        if (!string.IsNullOrWhiteSpace(query.Status) && query.Status != "All")
        {
            ordersQuery = ordersQuery.Where(o => o.OrderStatus == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string kw = query.Keyword.Trim();
            ordersQuery = ordersQuery.Where(o =>
                o.OrderCode.Contains(kw) ||
                o.SellerOrder.Any(so => so.OrderItem.Any(oi => oi.Product.ProductName.Contains(kw))));
        }

        int totalCount = await ordersQuery.CountAsync(cancellationToken);

        var orders = await ordersQuery
            .OrderByDescending(o => o.OrderDate)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(o => o.SellerOrder)
                .ThenInclude(so => so.FarmerProfile)
            .Include(o => o.SellerOrder)
                .ThenInclude(so => so.OrderItem)
                    .ThenInclude(oi => oi.Product)
            .Include(o => o.SellerOrder)
                .ThenInclude(so => so.OrderItem)
                    .ThenInclude(oi => oi.OrderItemBatchAllocation)
                        .ThenInclude(ba => ba.HarvestBatch)
            .Include(o => o.SellerOrder)
                .ThenInclude(so => so.Shipment)
            .ToListAsync(cancellationToken);

        var items = orders.Select(o => MapOrderSummary(o)).ToList();

        return new PagedResponse<OrderSummaryDto>(
            items, totalCount, query.Page, query.PageSize);
    }

    public async Task<OrderDetailDto?> GetOrderDetailAsync(
        long orderId, long? buyerId, CancellationToken cancellationToken)
    {
        var query = _db.Order
            .AsNoTracking()
            .Where(o => o.OrderId == orderId);

        if (buyerId.HasValue)
        {
            query = query.Where(o => o.BuyerUserId == buyerId.Value);
        }

        var order = await query
            .Include(o => o.SellerOrder)
                .ThenInclude(so => so.FarmerProfile)
            .Include(o => o.SellerOrder)
                .ThenInclude(so => so.OrderItem)
                    .ThenInclude(oi => oi.Product)
            .Include(o => o.SellerOrder)
                .ThenInclude(so => so.OrderItem)
                    .ThenInclude(oi => oi.OrderItemBatchAllocation)
                        .ThenInclude(ba => ba.HarvestBatch)
            .Include(o => o.SellerOrder)
                .ThenInclude(so => so.Shipment)
            .Include(o => o.SellerOrder)
                .ThenInclude(so => so.OrderStatusHistory)
            .Include(o => o.PaymentTransaction)
            .FirstOrDefaultAsync(cancellationToken);

        if (order == null) return null;

        var summary = MapOrderSummary(order);
        var paymentTx = order.PaymentTransaction.OrderByDescending(p => p.RequestedAt).FirstOrDefault();
        string paymentStatus = paymentTx?.PaymentStatus ?? (order.OrderStatus == "Cancelled" ? "Refunded" : "Paid");

        var timeline = order.SellerOrder
            .SelectMany(so => so.OrderStatusHistory)
            .OrderBy(h => h.OccurredAt)
            .Select(h => new OrderStatusTimelineDto(
                h.NewStatus, Utc(h.OccurredAt), h.Reason ?? "Cập nhật trạng thái đơn hàng"))
            .ToList();

        if (timeline.Count == 0)
        {
            timeline.Add(new OrderStatusTimelineDto("Pending", Utc(order.OrderDate), "Đã tạo đơn hàng"));
        }

        return new OrderDetailDto(
            summary.OrderId,
            summary.OrderCode,
            summary.OrderDate,
            summary.OrderStatus,
            summary.PaymentMethod,
            summary.DeliveryMethod,
            summary.TotalAmount,
            summary.DeliveryFee,
            summary.DiscountAmount,
            summary.FinalAmount,
            summary.RecipientName,
            summary.RecipientPhone,
            summary.ShippingAddress,
            summary.TrackingNumber,
            paymentStatus,
            order.ExpectedDeliveryDate.HasValue ? Utc(order.ExpectedDeliveryDate.Value) : null,
            summary.SellerOrders,
            timeline);
    }

    public async Task<OrderDetailDto?> GetOrderDetailByCodeAsync(
        string orderCode, long? buyerId, CancellationToken cancellationToken)
    {
        var foundId = await _db.Order
            .AsNoTracking()
            .Where(o => o.OrderCode == orderCode)
            .Select(o => (long?)o.OrderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (!foundId.HasValue || foundId.Value == 0) return null;
        return await GetOrderDetailAsync(foundId.Value, buyerId, cancellationToken);
    }

    public async Task MarkOrderNotificationsReadAsync(
        long orderId, long buyerId, CancellationToken cancellationToken)
    {
        var notifications = await _db.Notification
            .Where(n => n.UserId == buyerId && !n.IsRead && n.RelatedRecordType == "Order")
            .ToListAsync(cancellationToken);

        foreach (var notif in notifications)
        {
            notif.IsRead = true;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static OrderSummaryDto MapOrderSummary(Db.Order o)
    {
        var sellerGroups = o.SellerOrder.Select(so =>
        {
            string? tracking = so.Shipment.Select(s => s.TrackingNumber).FirstOrDefault(t => !string.IsNullOrEmpty(t));

            var itemDtos = so.OrderItem.Select(oi =>
            {
                var alloc = oi.OrderItemBatchAllocation.FirstOrDefault();
                string? batchCode = alloc?.HarvestBatch?.BatchCode;
                DateOnly? harvestDate = alloc?.HarvestBatch?.ActualHarvestDate ?? alloc?.HarvestBatch?.EstimatedHarvestDate;

                return new OrderItemDetailDto(
                    oi.OrderItemId,
                    oi.ProductId,
                    oi.Product?.ProductName ?? oi.ProductNameSnapshot ?? "Nông sản",
                    oi.Quantity,
                    oi.UnitSnapshot,
                    oi.UnitPriceSnapshot,
                    oi.SubTotal,
                    batchCode,
                    harvestDate,
                    "Vườn nhà",
                    false,
                    "🌱");
            }).ToList();

            return new SellerOrderGroupDto(
                so.SellerOrderId,
                so.FarmerProfileId,
                so.FarmerProfile?.FarmName ?? so.SellerNameSnapshot ?? "Nhà vườn",
                so.FarmerProfile?.Address,
                so.SellerOrderCode,
                so.Status,
                so.FinalAmount,
                itemDtos);
        }).ToList();

        string? overallTracking = sellerGroups
            .SelectMany(sg => o.SellerOrder.Where(s => s.SellerOrderId == sg.SellerOrderId).SelectMany(s => s.Shipment))
            .Select(s => s.TrackingNumber)
            .FirstOrDefault(t => !string.IsNullOrEmpty(t)) ?? "AGRI-EXPRESS";

        return new OrderSummaryDto(
            o.OrderId,
            o.OrderCode,
            Utc(o.OrderDate),
            o.OrderStatus,
            o.PaymentMethod ?? "VNPay QR",
            o.DeliveryMethod ?? "Giao hàng tiêu chuẩn",
            o.TotalAmount,
            o.DeliveryFee,
            o.DiscountAmount,
            o.FinalAmount,
            o.RecipientNameSnapshot,
            o.RecipientPhoneSnapshot,
            o.ShippingAddress,
            overallTracking,
            sellerGroups);
    }
}
