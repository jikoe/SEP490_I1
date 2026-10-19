using System.Text.Json;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Domain.Exceptions;
using AgriSustain.Domain.ValueObjects;
using Microsoft.Data.SqlClient;

namespace AgriSustain.Infrastructure.Repositories.Sql;

internal sealed partial class SqlThreeUcSession
{
    private readonly Dictionary<long, (decimal Available, decimal Reserved)> _readStock = new();
    /// <summary>
    /// Reads buyer-owned SQL cart/address rows. Only point balances remain an explicit process-
    /// local prerequisite fixture; DB-09 has not authorized a persistent loyalty schema.
    /// </summary>
    public async Task<CommerceContext> GetContextAsync(
        long buyerId, CancellationToken cancellationToken)
    {
        var items = await QueryAsync("""
            SELECT i.CartItemId,i.ProductId,i.Quantity,i.IsSelected
            FROM dbo.CartItem i JOIN dbo.Cart c ON c.CartId=i.CartId
            WHERE c.UserId=@Buyer AND i.PreOrderCampaignId IS NULL ORDER BY i.CartItemId
            """, reader => new CartLine((long)reader["CartItemId"], (long)reader["ProductId"],
                (decimal)reader["Quantity"], (bool)reader["IsSelected"]),
            cancellationToken, ("@Buyer", buyerId));
        var addresses = await QueryAsync("""
            SELECT * FROM dbo.DeliveryAddress WHERE UserId=@Buyer AND Status='Active'
            ORDER BY IsDefault DESC,DeliveryAddressId
            """, reader => new AddressSnapshot((string)reader["RecipientName"],
                (string)reader["RecipientPhone"], (string)reader["ProvinceCity"],
                (string)reader["Ward"], (string)reader["AddressDetail"],
                Text(reader, "ProvinceCode") ?? "", Text(reader, "WardCode") ?? "",
                Text(reader, "AddressCatalogVersion") ?? ""), cancellationToken, ("@Buyer", buyerId));
        var vouchers = await QueryAsync("SELECT VoucherCode FROM dbo.Voucher WHERE IsActive=1",
            reader => reader.GetString(0), cancellationToken);
        if (!Points.ContainsKey(buyerId))
        {
            // DEMO-POINTS: replace with the loyalty team's reservation contract after DB-09.
            long primary = await ScalarAsync("""
                SELECT COUNT_BIG(*) FROM dbo.[User] WHERE UserId=@Buyer
                    AND Username='demo3uc_retail'
                """, cancellationToken, ("@Buyer", buyerId));
            Points[buyerId] = primary == 1 ? 1000 : 300;
        }
        return new CommerceContext(items, addresses, Points[buyerId], vouchers);
    }

    /// <summary>
    /// Maps Product → FarmerProfile → User explicitly. SellerId is FarmerProfileId, not UserId;
    /// eligibility is derived from persisted approval, publication, seller and category states.
    /// </summary>
    public async Task<ProductOffer?> FindProductAsync(long id, CancellationToken cancellationToken) =>
        (await QueryAsync("""
            SELECT p.*,f.FarmName,f.UserId SellerUserId,f.VerificationStatus,
                u.Status SellerStatus,u.LockedUntil
            FROM dbo.Product p JOIN dbo.FarmerProfile f ON f.FarmerProfileId=p.FarmerProfileId
            JOIN dbo.[User] u ON u.UserId=f.UserId WHERE p.ProductId=@Id
            """, reader => new ProductOffer((long)reader["ProductId"],
                (long)reader["CategoryId"], (long)reader["FarmerProfileId"],
                (string)reader["FarmName"], (string)reader["ProductName"], (string)reader["Unit"],
                (decimal)reader["BaseRetailPrice"], reader["WholesalePrice"] is DBNull
                    ? null : (decimal)reader["WholesalePrice"],
                reader["WholesaleMinQuantity"] is DBNull ? null
                    : (decimal)reader["WholesaleMinQuantity"],
                (string)reader["ApprovalStatus"] == "Approved", (bool)reader["IsPublished"],
                (string)reader["VerificationStatus"] == "Approved"
                    && (string)reader["SellerStatus"] == "Active"
                    && (reader["LockedUntil"] is DBNull
                        || Utc(reader, "LockedUntil") <= _clock.GetUtcNow()),
                (long)reader["SellerUserId"]), cancellationToken, ("@Id", id))).SingleOrDefault();

    /// <summary>
    /// Restores current SQL stock counters and sellability metadata without reserving twice.
    /// Application FEFO remains responsible for selecting eligible allocations.
    /// </summary>
    private static HarvestBatch Batch(SqlDataReader reader) => HarvestBatch.RestorePersisted(
        new HarvestBatch((decimal)reader["AvailableQuantity"])
        {
            Id = (long)reader["HarvestBatchId"],
            ProductId = (long)reader["ProductId"],
            Code = (string)reader["BatchCode"],
            Status = (string)reader["Status"],
            ExpirationDate = reader["ExpirationDate"] is DBNull
                ? null : DateOnly.FromDateTime((DateTime)reader["ExpirationDate"])
        }, (decimal)reader["ReservedQuantity"]);

    /// <summary>
    /// Reads candidate lots using the approved ProductId index; no fixed batch is added to cart.
    /// </summary>
    public async Task<IReadOnlyList<HarvestBatch>> FindBatchesAsync(
        long productId, CancellationToken cancellationToken) =>
        await QueryAsync("SELECT * FROM dbo.HarvestBatch WHERE ProductId=@Product", Batch,
            cancellationToken, ("@Product", productId));

    /// <summary>
    /// Loads the final stock projection inside the placement transaction.
    /// </summary>
    public async Task<HarvestBatch?> FindBatchAsync(long id, CancellationToken cancellationToken)
    {
        var batch = (await QueryAsync("SELECT * FROM dbo.HarvestBatch WHERE HarvestBatchId=@Id",
            Batch, cancellationToken, ("@Id", id))).SingleOrDefault();
        if (batch != null)
        {
            _readStock[id] = (batch.AvailableQuantity, batch.ReservedQuantity);
        }
        return batch;
    }

    /// <summary>
    /// Computes quota from Applied UsedCount plus currently live Reserved redemptions. The
    /// write transaction's locks prevent two adapter processes consuming the same final quota.
    /// </summary>
    public async Task<VoucherOffer?> FindVoucherAsync(
        string code, CancellationToken cancellationToken) =>
        (await QueryAsync("""
            SELECT v.*,(SELECT COUNT(*) FROM dbo.VoucherRedemption r
                WHERE r.VoucherId=v.VoucherId AND r.Status='Reserved'
                    AND (r.ExpiresAt IS NULL OR r.ExpiresAt>@Now)) ReservedCount
            FROM dbo.Voucher v WHERE v.VoucherCode=@Code
            """, reader => new VoucherOffer((long)reader["VoucherId"],
                (string)reader["VoucherCode"], reader["FarmerProfileId"] is DBNull
                    ? null : (long)reader["FarmerProfileId"], (string)reader["DiscountType"],
                (decimal)reader["DiscountValue"], reader["MaxDiscountAmount"] is DBNull
                    ? null : (decimal)reader["MaxDiscountAmount"], (decimal)reader["MinOrderAmount"],
                reader["UsageLimit"] is DBNull ? null : (int)reader["UsageLimit"],
                (int)reader["UsedCount"], (int)reader["ReservedCount"],
                Utc(reader, "StartDate"), Utc(reader, "EndDate"), (bool)reader["IsActive"]),
            cancellationToken, ("@Code", code), ("@Now", _clock.GetUtcNow().UtcDateTime)))
            .SingleOrDefault();

    /// <summary>
    /// Restores the saved accepted quote JSON. The service independently enforces owner, expiry
    /// and fingerprint checks before placement. Invalid persisted JSON is a technical failure.
    /// </summary>
    public async Task<CheckoutQuote?> FindQuoteAsync(Guid id, CancellationToken cancellationToken)
    {
        var rows = await QueryAsync("""
            SELECT AcceptedSnapshotJson FROM dbo.RetailCheckoutQuote WHERE QuoteId=@Id
            """, reader => JsonSerializer.Deserialize<CheckoutQuote>(reader.GetString(0))
                ?? throw new InvalidDataException("Invalid saved quote snapshot."),
            cancellationToken, ("@Id", id));
        return rows.SingleOrDefault();
    }

    /// <summary>
    /// Persists the immutable quote and real address/catalog/allocation snapshot. This does not
    /// reserve stock or create a payment. Expired-row cleanup belongs to the maintenance owner.
    /// </summary>
    public async Task SaveQuoteAsync(CheckoutQuote quote, CancellationToken cancellationToken) =>
        await ExecuteAsync("""
            INSERT dbo.RetailCheckoutQuote
                (QuoteId,BuyerUserId,AcceptedSnapshotJson,Fingerprint,CreatedAt,ExpiresAt)
            VALUES(@Id,@Buyer,@Json,@Fingerprint,@Created,@Expiry)
            """, cancellationToken, ("@Id", quote.Id), ("@Buyer", quote.BuyerId),
            ("@Json", JsonSerializer.Serialize(quote)), ("@Fingerprint", quote.Fingerprint),
            ("@Created", _clock.GetUtcNow().UtcDateTime), ("@Expiry", quote.ExpiresAt.UtcDateTime));

    /// <summary>
    /// DEMO-DB06: request-key lookup is process-local, explicitly not durable after restart.
    /// Verifies the SQL order still exists; no unapproved key column or unique-quote rule is added.
    /// </summary>
    public async Task<RetailOrder?> FindOrderAsync(
        long buyerId, Guid requestKey, CancellationToken cancellationToken)
    {
        if (!RequestOrders.TryGetValue((buyerId, requestKey), out var order))
        {
            return null;
        }
        return await ScalarAsync("SELECT COUNT_BIG(*) FROM dbo.[Order] WHERE OrderId=@Id",
            cancellationToken, ("@Id", order.Id)) == 1 ? order : null;
    }

    /// <summary>
    /// Obtains a genuine SQL IDENTITY using an uncommitted placeholder tied to the actor. Final
    /// snapshots replace it in SaveOrderAsync; any failure before that step rolls it back.
    /// No MAX(id)+1, IDENTITY_INSERT, sequence-dependent fixture identity or schema change is used.
    /// </summary>
    public Task<long> NextOrderIdAsync(CancellationToken cancellationToken) => ScalarAsync("""
        INSERT dbo.[Order](BuyerUserId,OrderCode,OrderType,ShippingAddress,
            RecipientNameSnapshot,RecipientPhoneSnapshot,OrderStatus)
        OUTPUT inserted.OrderId
        VALUES(@Buyer,@Code,'Retail',N'',N'','','Initializing')
        """, cancellationToken, ("@Buyer", _actorId
            ?? throw new InvalidOperationException("Missing actor for order identity.")),
            ("@Code", "PENDING-" + Guid.NewGuid().ToString("N")));

    /// <summary>
    /// Moves only the calculated delta from SQL available to reserved, conditional on sufficient
    /// stock. This protects persisted counters even against a writer outside this demo adapter.
    /// All placement effects still share the enclosing SQL transaction.
    /// </summary>
    public async Task ReserveBatchAsync(HarvestBatch batch, CancellationToken cancellationToken)
    {
        if (!_readStock.TryGetValue(batch.Id, out var before))
        {
            throw new InvalidOperationException("Stock must be read before reservation.");
        }
        decimal quantity = before.Available - batch.AvailableQuantity;
        if (quantity <= 0 || batch.ReservedQuantity - before.Reserved != quantity)
        {
            throw new InvalidOperationException("Invalid stock reservation delta.");
        }
        long affected = await ScalarAsync("""
            DECLARE @Changed TABLE(Id bigint);
            UPDATE dbo.HarvestBatch
            SET AvailableQuantity=AvailableQuantity-@Quantity,
                ReservedQuantity=ReservedQuantity+@Quantity
            OUTPUT inserted.HarvestBatchId INTO @Changed
            WHERE HarvestBatchId=@Id AND Status='Available'
                AND AvailableQuantity=@BeforeAvailable AND ReservedQuantity=@BeforeReserved
                AND AvailableQuantity>=@Quantity
                AND (ExpirationDate IS NULL OR ExpirationDate>=@Today);
            SELECT COUNT_BIG(*) FROM @Changed;
            """, cancellationToken, ("@Id", batch.Id), ("@Quantity", quantity),
            ("@BeforeAvailable", before.Available), ("@BeforeReserved", before.Reserved),
            ("@Today", _clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date));
        if (affected != 1)
        {
            throw new BusinessRuleException("Tồn kho đã thay đổi. Vui lòng tính lại.");
        }
    }

    /// <summary>
    /// Persists the confirmed parent/seller/item/FEFO allocation snapshots. The following benefit,
    /// cart and audit writes must also succeed before the UoW commits this order.
    /// </summary>
    public async Task SaveOrderAsync(RetailOrder order, CancellationToken cancellationToken)
    {
        await ExecuteAsync("""
            UPDATE dbo.[Order] SET OrderCode=@Code,OrderStatus=@Status,OrderDate=@Created,
                ExpectedDeliveryDate=NULL,ShippingAddress=@Shipping,TotalAmount=@Subtotal,
                DiscountAmount=@Discount,DeliveryFee=@Fee,FinalAmount=@Total,
                ReservationExpiresAt=@Expiry,RecipientNameSnapshot=@Recipient,
                RecipientPhoneSnapshot=@Phone,RetailQuoteId=@Quote,DeliveryMethod=@Delivery,
                PaymentMethod=@Payment,OrderNotes=@Notes,VoucherCodeSnapshot=
                    (SELECT VoucherCode FROM dbo.Voucher WHERE VoucherId=@Voucher),
                VoucherDiscount=@VoucherDiscount,PointDiscount=@PointsDiscount,
                RedeemedPoints=@Points,ProvinceCodeSnapshot=@ProvinceCode,
                ProvinceNameSnapshot=@Province,WardCodeSnapshot=@WardCode,WardNameSnapshot=@Ward,
                AddressDetailSnapshot=@Detail,AddressCatalogVersionSnapshot=@Catalog
            WHERE OrderId=@Id AND BuyerUserId=@Buyer
            """, cancellationToken, ("@Id", order.Id), ("@Buyer", order.BuyerId),
            ("@Code", order.Code), ("@Status", order.Status),
            ("@Created", order.CreatedAt.UtcDateTime),
            ("@Shipping", $"{order.Address.Detail}, {order.Address.Ward}, {order.Address.Province}"),
            ("@Subtotal", order.Subtotal), ("@Discount", order.VoucherDiscount + order.PointsDiscount),
            ("@Fee", order.DeliveryFee), ("@Total", order.Total),
            ("@Expiry", order.ReservationExpiresAt.UtcDateTime),
            ("@Recipient", order.Address.RecipientName), ("@Phone", order.Address.Phone),
            ("@Quote", order.QuoteId), ("@Delivery", order.DeliveryMethod),
            ("@Payment", order.PaymentMethod), ("@Notes", order.Notes), ("@Voucher", order.VoucherId),
            ("@VoucherDiscount", order.VoucherDiscount), ("@PointsDiscount", order.PointsDiscount),
            ("@Points", order.RedeemedPoints), ("@ProvinceCode", order.Address.ProvinceCode),
            ("@Province", order.Address.Province), ("@WardCode", order.Address.WardCode),
            ("@Ward", order.Address.Ward), ("@Detail", order.Address.Detail),
            ("@Catalog", order.Address.CatalogVersion));
        foreach (var seller in order.Sellers)
        {
            string name = order.Lines.First(line => line.SellerId == seller.SellerId).SellerName;
            long sellerOrderId = await ScalarAsync("""
                INSERT dbo.SellerOrder(OrderId,FarmerProfileId,SellerOrderCode,Subtotal,
                    DiscountShare,DeliveryFee,FinalAmount,Status,SellerNameSnapshot)
                OUTPUT inserted.SellerOrderId VALUES(@Order,@Seller,@Code,@Subtotal,
                    @Discount,@Fee,@Total,'PendingPayment',@Name)
                """, cancellationToken, ("@Order", order.Id), ("@Seller", seller.SellerId),
                ("@Code", seller.Code), ("@Subtotal", seller.Subtotal),
                ("@Discount", seller.DiscountShare), ("@Fee", seller.DeliveryFee),
                ("@Total", seller.FinalAmount), ("@Name", name));
            foreach (var line in order.Lines.Where(line => line.SellerId == seller.SellerId))
            {
                long itemId = await ScalarAsync("""
                    INSERT dbo.OrderItem(SellerOrderId,ProductId,Quantity,UnitPriceSnapshot,
                        UnitSnapshot,SubTotal,ProductNameSnapshot)
                    OUTPUT inserted.OrderItemId
                    VALUES(@SellerOrder,@Product,@Quantity,@Price,@Unit,@Subtotal,@Name)
                    """, cancellationToken, ("@SellerOrder", sellerOrderId),
                    ("@Product", line.ProductId), ("@Quantity", line.Quantity),
                    ("@Price", line.UnitPrice), ("@Unit", line.Unit),
                    ("@Subtotal", line.Subtotal), ("@Name", line.ProductName));
                int sequence = 0;
                foreach (var allocation in line.Allocations)
                {
                    await ExecuteAsync("""
                        INSERT dbo.OrderItemBatchAllocation(OrderItemId,HarvestBatchId,
                            AllocationSequence,AllocatedQuantity,AllocationStatus,ReservedAt,ExpiresAt)
                        VALUES(@Item,@Batch,@Sequence,@Quantity,'Reserved',@At,@Expiry)
                        """, cancellationToken, ("@Item", itemId), ("@Batch", allocation.BatchId),
                        ("@Sequence", ++sequence), ("@Quantity", allocation.Quantity),
                        ("@At", order.CreatedAt.UtcDateTime),
                        ("@Expiry", order.ReservationExpiresAt.UtcDateTime));
                }
            }
        }
        RequestOrders[(order.BuyerId, order.RequestKey)] = order;
    }

    /// <summary>
    /// Inserts a durable Reserved voucher redemption with expiry; UsedCount is not incremented
    /// before payment. Points remain a mock until DB-09; apply/release/payment/expiry workers are
    /// intentionally not implemented here. Failed SQL callbacks discard staged mock changes.
    /// </summary>
    public async Task ReserveBenefitsAsync(RetailOrder order, CancellationToken cancellationToken)
    {
        int balance = Points.GetValueOrDefault(order.BuyerId);
        if (balance < order.RedeemedPoints)
        {
            throw new BusinessRuleException("Điểm khả dụng đã thay đổi.");
        }
        Points[order.BuyerId] = balance - order.RedeemedPoints;
        if (order.VoucherId is { } voucherId)
        {
            await ExecuteAsync("""
                INSERT dbo.VoucherRedemption
                    (VoucherId,OrderId,DiscountSnapshot,Status,ReservedAt,ExpiresAt)
                VALUES(@Voucher,@Order,@Discount,'Reserved',@At,@Expiry)
                """, cancellationToken, ("@Voucher", voucherId), ("@Order", order.Id),
                ("@Discount", order.VoucherDiscount), ("@At", order.CreatedAt.UtcDateTime),
                ("@Expiry", order.ReservationExpiresAt.UtcDateTime));
        }
    }

    /// <summary>
    /// Deletes only the exact selected buyer-owned cart rows used by the accepted quote. Any
    /// later failure rolls back removals; unselected rows and other buyers' carts remain intact.
    /// </summary>
    public async Task RemoveOrderedItemsAsync(
        long buyerId, IReadOnlyList<long> itemIds, CancellationToken cancellationToken)
    {
        foreach (long id in itemIds)
        {
            await ExecuteAsync("""
                DELETE i FROM dbo.CartItem i JOIN dbo.Cart c ON c.CartId=i.CartId
                WHERE c.UserId=@Buyer AND i.CartItemId=@Id AND i.IsSelected=1
                """, cancellationToken, ("@Buyer", buyerId), ("@Id", id));
        }
    }
}
