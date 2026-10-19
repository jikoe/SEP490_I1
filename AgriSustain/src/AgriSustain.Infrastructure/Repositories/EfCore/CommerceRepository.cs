using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Commerce;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Services.Commerce;
using AgriSustain.Infrastructure.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using D = AgriSustain.Infrastructure.Data.Entities;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

internal sealed class CommerceRepository(AgriSustainDbContext db, TimeProvider clock, CancellationToken ct) : ICommerceRepository
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date);
    public Task<long?> SubmissionAsync(long actorId, Guid key) => db.Set<D.CommerceSubmission>()
        .Where(x => x.ActorId == actorId && x.RequestKey == key).Select(x => (long?)x.ProductId).SingleOrDefaultAsync(ct);
    public Task RecordSubmissionAsync(long actorId, Guid key, long productId)
    {
        db.Add(new D.CommerceSubmission {ActorId = actorId, RequestKey = key, ProductId = productId});
        return Task.CompletedTask;
    }
    public async Task<CommerceActor?> ActorAsync(long id)
    {
        var u = await db.User.Include(x => x.FarmerProfile).SingleOrDefaultAsync(x => x.UserId == id, ct);
        return u is null ? null : new(u.UserId, u.Role, u.Status, u.FarmerProfile?.FarmerProfileId,
            u.FarmerProfile?.VerificationStatus == "Approved", u.LockedUntil > Now);
    }
    public Task<CategoryOption[]> CategoriesAsync() => db.Category.Where(x => x.IsActive).OrderBy(x => x.CategoryName)
        .Select(x => new CategoryOption(x.CategoryId, x.CategoryName)).ToArrayAsync(ct);
    public Task<bool> CategoryActiveAsync(long id) => db.Category.AnyAsync(x => x.CategoryId == id && x.IsActive, ct);
    private IQueryable<D.Product> ProductGraph() => db.Product.Include(x => x.Category).Include(x => x.FarmerProfile).ThenInclude(x => x.User)
        .Include(x => x.ProductImage).Include(x => x.HarvestBatch).AsSplitQuery();
    public async Task<PagedResponse<Listing>> ListingsAsync(long? farmId, CommerceQuery query)
    {
        var q = ProductGraph();
        q = farmId.HasValue ? q.Where(p => p.FarmerProfileId == farmId) : q.Where(p => p.ApprovalStatus == "Approved" && p.IsPublished
            && p.Category.IsActive && p.FarmerProfile.VerificationStatus == "Approved" && p.FarmerProfile.User.Status == "Active"
            && (p.FarmerProfile.User.LockedUntil == null || p.FarmerProfile.User.LockedUntil <= Now));
        if (!string.IsNullOrWhiteSpace(query.Keyword)) q = q.Where(p => p.ProductName.Contains(query.Keyword));
        if (!string.IsNullOrWhiteSpace(query.Status)) q = q.Where(p => p.ApprovalStatus == query.Status);
        if (query.Published.HasValue) q = q.Where(p => p.IsPublished == query.Published);
        if (query.CategoryId.HasValue) q = q.Where(p => p.CategoryId == query.CategoryId);
        if (query.From.HasValue) q = q.Where(p => p.CreatedAt >= query.From.Value.Date.AddHours(-7));
        if (query.To.HasValue) q = q.Where(p => p.CreatedAt < query.To.Value.Date.AddDays(1).AddHours(-7));
        int count = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(p => p.ProductId).Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArrayAsync(ct);
        return new(rows.Select(Map).ToArray(), count, query.Page, query.PageSize);
    }
    public async Task<Listing?> ListingAsync(long id)
    {
        var p = await ProductGraph().SingleOrDefaultAsync(x => x.ProductId == id, ct);
        if (p is null) return null;
        var view = Map(p);
        view.UnitLocked = p.HarvestBatch.Count > 0 || await db.OrderItem.AnyAsync(x => x.ProductId == id, ct);
        return view;
    }
    private static BatchOffer Batch(D.HarvestBatch b) => new(b.HarvestBatchId, b.ProductId, b.BatchCode,
        b.AvailableQuantity, b.ReservedQuantity, b.Status, b.ActualHarvestDate, b.ExpirationDate, b.StockEnteredAt is not null);
    private Listing Map(D.Product p) => new() {
        Id = p.ProductId, FarmId = p.FarmerProfileId, FarmName = p.FarmerProfile.FarmName, Name = p.ProductName,
        CategoryId = p.CategoryId, CategoryActive = p.Category.IsActive, Unit = p.Unit, QuantityStep = p.QuantityStep,
        Origin = p.OriginProvince ?? "", Description = p.Description ?? "", RetailPrice = p.BaseRetailPrice,
        WholesaleMinimum = p.WholesaleMinQuantity, WholesalePrice = p.WholesalePrice, Approval = p.ApprovalStatus,
        Published = p.IsPublished, FarmEligible = p.FarmerProfile.VerificationStatus == "Approved" && p.FarmerProfile.User.Status == "Active"
            && (p.FarmerProfile.User.LockedUntil is null || p.FarmerProfile.User.LockedUntil <= Now),
        RejectionReason = p.RejectionReason, Revision = p.Revision,
        Images = p.ProductImage.OrderBy(x => x.DisplayOrder).Select(x => new ImageView(x.ImageUrl, x.IsPrimary)).ToArray(),
        Batches = p.HarvestBatch.Select(Batch).ToArray(),
        AvailableQuantity = p.HarvestBatch.Where(b => CommerceRules.BatchEligible(Batch(b), Today)).Sum(b => b.AvailableQuantity)
    };
    public async Task<long> SaveListingAsync(Listing p, ImageInput[]? images, long actorId, string action)
    {
        var row = p.Id == 0 ? new D.Product { FarmerProfileId = p.FarmId, CreatedAt = Now } : await db.Product.FindAsync([p.Id], ct)
            ?? throw new DbUpdateConcurrencyException();
        if (p.Id == 0) db.Product.Add(row);
        string before = JsonSerializer.Serialize(new {row.ProductName, row.ApprovalStatus, row.IsPublished, row.BaseRetailPrice, row.Revision});
        row.ProductName = p.Name; row.CategoryId = p.CategoryId; row.Unit = p.Unit; row.OriginProvince = p.Origin;
        row.Description = p.Description; row.BaseRetailPrice = p.RetailPrice; row.WholesaleMinQuantity = p.WholesaleMinimum;
        row.WholesalePrice = p.WholesalePrice; row.ApprovalStatus = p.Approval; row.IsPublished = p.Published;
        row.QuantityStep = p.QuantityStep; row.Revision = p.Revision;
        row.RejectionReason = p.RejectionReason;
        if (images is not null)
        {
            var old = await db.ProductImage.Where(x => x.ProductId == p.Id).ToArrayAsync(ct);
            db.ProductImage.RemoveRange(old);
            int index = 0;
            foreach (var image in images)
            {
                string url = image.Content is null ? image.ExistingUrl! : Media(image, actorId, true);
                db.ProductImage.Add(new D.ProductImage { Product = row, ImageUrl = url, DisplayOrder = index++, IsPrimary = image.Primary, AltText = p.Name });
            }
            // Old media is removed only when no retained image references it; rollback restores it.
            foreach (var oldImage in old.Where(o => !images.Any(i => i.ExistingUrl == o.ImageUrl)))
                if (Guid.TryParse(oldImage.ImageUrl.Split('/').Last(), out var mediaId))
                {
                    var media = await db.Set<D.CommerceMedia>().FindAsync([mediaId], ct);
                    if (media?.OwnerUserId == actorId) db.Remove(media);
                }
        }
        await db.SaveChangesAsync(ct); // Generates ID inside the transaction, not a partial commit.
        Audit(actorId, "Product", row.ProductId, action, before, JsonSerializer.Serialize(new {p.Approval, p.Published, p.RetailPrice, p.Revision}));
        if (action is "Approve" or "Reject")
        {
            long owner = await db.FarmerProfile.Where(f => f.FarmerProfileId == row.FarmerProfileId).Select(f => f.UserId).SingleAsync(ct);
            Notify(owner, action == "Approve" ? "MSG29" : "MSG165", row.ProductName + ": " + p.Approval + (p.RejectionReason is null ? "" : ": " + p.RejectionReason));
        }
        return row.ProductId;
    }
    private string Media(ImageInput input, long owner, bool publicImage)
    {
        var id = Guid.NewGuid();
        db.Add(new D.CommerceMedia { Id = id, OwnerUserId = owner, Content = input.Content!, ContentType = input.ContentType, PublicProductImage = publicImage });
        return "/api/product-media/" + id;
    }
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
    private IQueryable<D.SellerOrder> OrderGraph() => db.SellerOrder.Include(x => x.Order).ThenInclude(x => x.PaymentTransaction)
        .Include(x => x.Order).ThenInclude(x => x.BuyerUser)
        .Include(x => x.FarmerProfile).Include(x => x.OrderItem).ThenInclude(x => x.OrderItemBatchAllocation).ThenInclude(x => x.HarvestBatch)
        .Include(x => x.OrderStatusHistory).AsSplitQuery();
    public async Task<PagedResponse<FulfillmentOrder>> OrdersAsync(long actorId, bool buyer, CommerceQuery query)
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
        EvidenceRequests = await db.Set<D.CommerceEvidenceRequest>().Where(e => e.RequestedUserId == actorId
            && db.DisputeTicket.Any(t => t.DisputeTicketId == e.DisputeTicketId && t.SellerOrderId == x.SellerOrderId))
            .Select(e => new EvidenceRequestView(e.Id, e.DisputeTicketId, e.RequestedAt, e.RespondedAt, e.Response)).ToArrayAsync(ct),
        RefundPending = await db.Set<D.CommerceRefundRequest>().AnyAsync(r => r.SellerOrderId == x.SellerOrderId && r.Status == "PendingFinance", ct)
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
            db.Add(new D.CommerceRefundRequest { SellerOrderId = row.SellerOrderId, Reason = reason!, CreatedAt = Now });
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
            var request = await db.Set<D.CommerceEvidenceRequest>().SingleAsync(x => x.Id == input.RequestId && x.RequestedUserId == actorId, ct);
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
    private void Notify(long userId, string code, string text) => db.Notification.Add(new D.Notification {
        UserId = userId, Title = code, Content = text, NotificationType = code, DeliveryChannel = "InApp", DeliveryStatus = "Queued", SentAt = Now, RelatedRecordType = "SellerOrder" });
    private void Audit(long actor, string entity, long id, string action, string? oldValue, string newValue, string? reason = null) =>
        db.AuditLog.Add(new D.AuditLog { PerformedByUserId = actor, EntityType = entity, EntityId = id,
            Action = action, OldValue = oldValue, NewValue = newValue, Reason = reason, OccurredAt = Now });
}
