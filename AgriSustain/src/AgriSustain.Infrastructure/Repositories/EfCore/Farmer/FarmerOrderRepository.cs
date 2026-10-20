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
    private IQueryable<D.SellerOrder> OrderGraph() => db.SellerOrder.Include(x => x.Order).ThenInclude(x => x.PaymentTransaction)
        .Include(x => x.Order).ThenInclude(x => x.BuyerUser)
        .Include(x => x.FarmerProfile).Include(x => x.OrderItem).ThenInclude(x => x.OrderItemBatchAllocation).ThenInclude(x => x.HarvestBatch)
        .Include(x => x.OrderStatusHistory).AsSplitQuery();
    public async Task<PagedResponse<FulfillmentOrder>> OrdersAsync(long actorId, bool buyer, FarmerRetailQuery query)
    {
        var q = OrderGraph().Where(x => buyer ? x.Order.BuyerUserId == actorId : x.FarmerProfile.UserId == actorId);
        if (!string.IsNullOrWhiteSpace(query.Keyword)) q = q.Where(x => x.SellerOrderCode.Contains(query.Keyword)
            || x.Order.OrderCode.Contains(query.Keyword) || x.Order.RecipientNameSnapshot.Contains(query.Keyword)
            || x.OrderItem.Any(l => l.OrderItemBatchAllocation.Any(a => a.HarvestBatch.BatchCode.Contains(query.Keyword))));
        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(x => x.Status == query.Status);
        if (query.From.HasValue) q = q.Where(x => x.Order.OrderDate >= query.From.Value.Date.AddHours(-7));
        if (query.To.HasValue) q = q.Where(x => x.Order.OrderDate < query.To.Value.Date.AddDays(1).AddHours(-7));
        if (!string.IsNullOrWhiteSpace(query.OrderType)) q = q.Where(x => x.Order.OrderType == query.OrderType);
        if (query.Paid.HasValue) q = q.Where(x => x.Order.PaymentTransaction.Any(p => p.PaidAt != null && (p.PaymentStatus == "Paid" || p.PaymentStatus == "Success" || p.PaymentStatus == "Reconciled")) == query.Paid);
        int count = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.SellerOrderId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        var result = new List<FulfillmentOrder>();
        foreach (var row in rows) result.Add(await MapOrder(row, actorId));
        return new(result, count, query.Page, query.PageSize);
    }
    public async Task<FulfillmentOrder?> OrderAsync(long id, long actorId, bool buyer)
    {
        var row = await OrderGraph().SingleOrDefaultAsync(x => x.SellerOrderId == id
            && (buyer ? x.Order.BuyerUserId == actorId : x.FarmerProfile.UserId == actorId), ct);
        return row is null ? null : await MapOrder(row, actorId);
    }
    private async Task<FulfillmentOrder> MapOrder(D.SellerOrder x, long actorId) => new() {
        FarmName = x.FarmerProfile.FarmName, FarmVerified = x.FarmerProfile.VerificationStatus == "Approved",
        BuyerName = x.Order.BuyerUser.FullName,
        PayoutStatuses = await db.PayoutItem.Where(p => p.OrderItem != null && p.OrderItem.SellerOrderId == x.SellerOrderId)
            .Select(p => p.FarmerPayout.PayoutStatus).Distinct().ToArrayAsync(ct),
        Complaints = await db.DisputeTicket.Where(t => t.SellerOrderId == x.SellerOrderId)
            .Select(t => new ComplaintView(t.DisputeTicketId, t.TicketStatus,
                t.RefundTransaction.Select(r => r.RefundStatus).ToArray())).ToArrayAsync(ct),
        Id = x.SellerOrderId, ParentId = x.OrderId, FarmId = x.FarmerProfileId, BuyerId = x.Order.BuyerUserId,
        Code = x.SellerOrderCode, ParentCode = x.Order.OrderCode, Type = x.Order.OrderType, PreOrder = x.Order.IsPreOrder,
        ContractOrder = x.B2BContractId is not null, Status = x.Status, Total = x.FinalAmount,
        RequiredPayment = x.RequiredPaymentAmount ?? x.Order.FinalAmount,
        VerifiedPayment = x.Order.PaymentTransaction.Where(p => p.PaidAt is not null && p.PaymentStatus is "Paid" or "Success" or "Reconciled").Sum(p => p.Amount),
        CreatedAt = x.Order.OrderDate, DeliveredAt = x.DeliveredAt, ReceivedAt = x.ReceivedAt,
        ExpectedDelivery = x.Order.ExpectedDeliveryDate is { } d ? DateOnly.FromDateTime(d) : null,
        Recipient = x.Order.RecipientNameSnapshot, Phone = x.Order.RecipientPhoneSnapshot, Address = x.Order.ShippingAddress, Notes = x.Order.OrderNotes,
        Lines = x.OrderItem.Select(l => new FulfillmentLine(l.OrderItemId, l.ProductId, l.ProductNameSnapshot ?? "Nông sản", l.UnitSnapshot,
            l.Quantity, l.UnitPriceSnapshot, l.OrderItemBatchAllocation.Select(a => new AllocationView(a.OrderItemBatchAllocationId,
                a.AllocatedQuantity, a.AllocationStatus, Batch(a.HarvestBatch))).ToArray())).ToArray(),
        History = x.OrderStatusHistory.OrderBy(h => h.OccurredAt).Select(h => new HistoryView(h.NewStatus, h.Reason, h.OccurredAt)).ToArray(),
        EvidenceRequests = await db.Set<D.OrderEvidenceRequest>().Where(e => e.RequestedUserId == actorId
            && db.DisputeTicket.Any(t => t.DisputeTicketId == e.DisputeTicketId && t.SellerOrderId == x.SellerOrderId))
            .Select(e => new EvidenceRequestView(e.Id, e.DisputeTicketId, e.RequestedAt, e.RespondedAt, e.Response)).ToArrayAsync(ct),
        RefundPending = await db.Set<D.OrderRefundRequest>().AnyAsync(r => r.SellerOrderId == x.SellerOrderId && r.Status == "PendingFinance", ct)
    };
    public async Task SaveOrderAsync(FulfillmentOrder view, long actorId, string action, string? reason = null)
    {
        var row = await OrderGraph().SingleAsync(x => x.SellerOrderId == view.Id, ct);
        string before = row.Status;
        row.Status = view.Status; row.DeliveredAt = view.DeliveredAt; row.ReceivedAt = view.ReceivedAt;
        if (action is "Dispatch" or "Reject")
        {
            foreach (var allocation in row.OrderItem.SelectMany(i => i.OrderItemBatchAllocation).OrderBy(a => a.HarvestBatchId))
            {
                if (allocation.AllocationStatus is not ("Reserved" or "Committed")) continue;
                var batch = allocation.HarvestBatch;
                if (batch.ReservedQuantity < allocation.AllocatedQuantity) throw new DbUpdateConcurrencyException();
                batch.ReservedQuantity -= allocation.AllocatedQuantity;
                if (action == "Reject") batch.AvailableQuantity += allocation.AllocatedQuantity;
                allocation.AllocationStatus = action == "Dispatch" ? "Consumed" : "Released";
                if (action == "Reject") allocation.ReleasedAt = Now;
                db.InventoryMovement.Add(new D.InventoryMovement { HarvestBatchId = batch.HarvestBatchId,
                    OrderItemBatchAllocationId = allocation.OrderItemBatchAllocationId, RecordedByUserId = actorId,
                    MovementType = action == "Dispatch" ? "Shipment" : "ReservationRelease",
                    QuantityChange = action == "Dispatch" ? -allocation.AllocatedQuantity : 0,
                    Reason = reason ?? view.Code, OccurredAt = Now });
            }
        }
        if (action == "Dispatch")
        {
            var shipment = new D.Shipment { SellerOrder = row, ShippedAt = Now, ShipmentStatus = "InTransit" };
            db.Shipment.Add(shipment);
            foreach (var a in row.OrderItem.SelectMany(i => i.OrderItemBatchAllocation))
                db.ShipmentItem.Add(new D.ShipmentItem { Shipment = shipment, OrderItemBatchAllocationId = a.OrderItemBatchAllocationId,
                    ShippedQuantity = a.AllocatedQuantity, ReconciliationStatus = "Pending" });
        }
        if (action is "Receive" or "Accept")
        {
            foreach (var s in await db.Shipment.Where(x => x.SellerOrderId == row.SellerOrderId).ToArrayAsync(ct))
            {
                s.ShipmentStatus = view.Status;
                if (action == "Receive") s.DeliveredAt = Now; else s.ReceivedAt = Now;
            }
        }
        if (action == "Reject" && view.VerifiedPayment > 0)
            db.Add(new D.OrderRefundRequest { SellerOrderId = row.SellerOrderId, Reason = reason!, CreatedAt = Now });
        db.OrderStatusHistory.Add(new D.OrderStatusHistory { SellerOrderId = row.SellerOrderId,
            ChangedByUserId = actorId, OldStatus = before, NewStatus = view.Status, Reason = reason, OccurredAt = Now });
        Audit(actorId, "SellerOrder", row.SellerOrderId, action, before, view.Status, reason);
        Notify(action is "Receive" or "Accept" ? row.FarmerProfile.UserId : row.Order.BuyerUserId,
            action == "Dispatch" ? "MSG65" : action is "Receive" or "Accept" ? "MSG147" : action == "Reject" ? "MSG67" : "OrderUpdate", view.Code + ": " + view.Status);
        if (action == "Reject" && view.VerifiedPayment > 0) Notify(row.FarmerProfile.UserId, "MSG162", view.Code + ": hoàn tiền chờ Finance xử lý.");
        var siblings = await db.SellerOrder.Where(x => x.OrderId == row.OrderId).ToArrayAsync(ct);
        row.Order.OrderStatus = siblings.All(x => x.Status == "Cancelled") ? "Cancelled"
            : siblings.All(x => x.Status is "Delivered" or "Received" or "Cancelled") ? "Completed"
            : siblings.Any(x => x.Status is "Shipping" or "InTransit" or "Delivered" or "Received") ? "PartiallyShipped" : "Processing";
        if (row.Order.OrderStatus == "Completed") row.Order.CompletedAt = Now;
    }
    public async Task SaveEvidenceAsync(FulfillmentOrder order, long actorId, EvidenceInput input, bool failure)
    {
        D.DisputeTicket ticket;
        if (failure)
        {
            ticket = new D.DisputeTicket { SellerOrderId = order.Id, SubmittedByUserId = actorId,
                DisputeReason = "DeliveryFailureOrReturn", Description = input.Description, TicketStatus = "Open", CreatedAt = Now };
            db.DisputeTicket.Add(ticket);
            foreach (var s in await db.Shipment.Where(s => s.SellerOrderId == order.Id).ToArrayAsync(ct)) s.ShipmentStatus = "DeliveryFailed";
            db.OrderStatusHistory.Add(new D.OrderStatusHistory { SellerOrderId = order.Id, ChangedByUserId = actorId,
                OldStatus = order.Status, NewStatus = order.Status, Reason = "DeliveryFailureOrReturn: " + input.Description, OccurredAt = Now });
        }
        else
        {
            var request = await db.Set<D.OrderEvidenceRequest>().SingleAsync(x => x.Id == input.RequestId && x.RequestedUserId == actorId, ct);
            request.RespondedAt = Now; request.Response = input.Description;
            ticket = await db.DisputeTicket.SingleAsync(x => x.DisputeTicketId == request.DisputeTicketId && x.SellerOrderId == order.Id, ct);
        }
        foreach (var file in input.Files)
            db.DisputeEvidence.Add(new D.DisputeEvidence { DisputeTicket = ticket, SubmittedByUserId = actorId,
                FileUrl = Media(file, actorId, false), Description = input.Description, SubmittedAt = Now });
        // A text-only response is retained on the evidence request; original evidence remains immutable.
        Audit(actorId, "SellerOrder", order.Id, failure ? "DeliveryIssue" : "EvidenceResponse", null, input.Description);
        Notify(order.BuyerId, failure ? "MSG149" : "MSG150", order.Code + ": hồ sơ đang chờ hỗ trợ xem xét.");
    }
}
