using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Application.Mappings;
using AgriSustain.Domain.Entities.Accounts;
using AgriSustain.Domain.Entities.Catalog;
using AgriSustain.Domain.Entities.Ordering;
using AgriSustain.Domain.ValueObjects;
using AgriSustain.Infrastructure.Data.Demo;

namespace AgriSustain.Infrastructure.Repositories.Demo;

// DEMO-PERSISTENCE: this adapter operates only on the UoW's isolated working snapshot.
// Replace each explicit repository implementation with SQL queries (filter/page in SQL),
// keeping mutation + audit + cart/benefit reservation in the same scoped transaction.
/// <summary>
/// Binds all repositories to one isolated demo snapshot, making mutation, reservation,
/// selected-cart removal and audit share a commit boundary.
/// </summary>
internal sealed class DemoSession(DemoState state) :
    IThreeUcSession, IAccountRepository, ICategoryRepository, ICheckoutRepository,
    IAuthRepository, IProfileRepository, IOrderTrackingRepository
{
    public IAccountRepository Accounts => this;
    public ICategoryRepository Categories => this;
    public ICheckoutRepository Checkout => this;
    public IAuthRepository Auth => this;
    public IProfileRepository Profiles => this;
    public IOrderTrackingRepository Orders => this;

    /// <summary>
    /// Reads an account or category only from this unit-of-work working copy; missing IDs yield
    /// null and cannot expose another store instance.
    /// </summary>
    Task<UserAccount?> IAccountRepository.FindAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Accounts.GetValueOrDefault(id));

    /// <summary>
    /// Filters and pages the isolated fixture collection and projects approved response fields.
    /// SQL replacements must perform these operations in SQL rather than load whole tables.
    /// </summary>
    public Task<PagedResponse<UserResponse>> SearchAsync(
        UserQuery query, DateTimeOffset now, CancellationToken cancellationToken)
    {
        IEnumerable<UserAccount> users = state.Accounts.Values;
        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string keyword = query.Keyword.Trim();
            users = users.Where(user => new[] { user.Id.ToString(), user.Name, user.Email,
                user.Phone, user.OrganizationName }.Any(value =>
                    value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true));
        }

        users = users.Where(user => (query.Role == null || user.Role == query.Role)
            && (query.Status == null || user.Status == query.Status));
        UserAccount[] filtered = users.OrderBy(user => user.Id).ToArray();
        return Task.FromResult(new PagedResponse<UserResponse>(filtered
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize)
            .Select(user => UserMappings.ToResponse(user, now)).ToArray(),
            filtered.Length, query.Page, query.PageSize));
    }

    /// <summary>
    /// Stages a changed account/category in the isolated working copy. Publication to the shared
    /// demo store happens only if the enclosing operation and audit succeed.
    /// </summary>
    public Task SaveAsync(UserAccount account, CancellationToken cancellationToken)
    {
        state.Accounts[account.Id] = account;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads an account or category only from this unit-of-work working copy; missing IDs yield
    /// null and cannot expose another store instance.
    /// </summary>
    Task<Category?> ICategoryRepository.FindAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Categories.GetValueOrDefault(id));

    /// <summary>
    /// Filters and pages the isolated fixture collection and projects approved response fields.
    /// SQL replacements must perform these operations in SQL rather than load whole tables.
    /// </summary>
    public Task<PagedResponse<CategoryResponse>> SearchAsync(
        CategoryQuery query, CancellationToken cancellationToken)
    {
        var categories = state.Categories.Values.Where(category =>
            (query.Id == null || category.Id == query.Id)
            && (query.IsActive == null || category.IsActive == query.IsActive)
            && (!query.RootOnly || category.ParentId == null)
            && (query.ParentId == null || category.ParentId == query.ParentId)
            && (string.IsNullOrWhiteSpace(query.Keyword)
                || category.Name.Contains(
                    query.Keyword.Trim(), StringComparison.OrdinalIgnoreCase)))
            .OrderBy(category => category.Priority).ThenBy(category => category.Id).ToArray();
        return Task.FromResult(new PagedResponse<CategoryResponse>(categories
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(category => new
                CategoryResponse(category.Id, category.Name, category.ParentId,
                    category.ParentId is { } parentId
                        ? state.Categories.GetValueOrDefault(parentId)?.Name : null,
                    category.Description, category.Priority, category.IsActive,
                    state.Products.Values.Count(product => product.CategoryId == category.Id)))
            .ToArray(),
            categories.Length, query.Page, query.PageSize));
    }

    /// <summary>
    /// Checks every fixture offer referencing the category, including unpublished/unapproved
    /// offers; a referenced category can only be hidden.
    /// </summary>
    public Task<bool> HasProductsAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Products.Values.Any(product => product.CategoryId == id));

    /// <summary>
    /// Checks fixture child categories and the mock benchmark/commission reference set. The SQL
    /// adapter must query the real referencing tables and retain their FKs.
    /// </summary>
    public Task<bool> HasOtherReferencesAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Categories.Values.Any(category => category.ParentId == id)
            || state.CategoryPolicyReferences.Contains(id));

    /// <summary>
    /// Removes the category from the isolated working copy. Failure of the later audit prevents
    /// the copy from being committed, restoring the original category.
    /// </summary>
    public Task DeleteAsync(long id, CancellationToken cancellationToken)
    {
        state.Categories.Remove(id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Advances the working-copy category ID counter only. SQL must use IDENTITY; this is not a
    /// cross-process ID generator.
    /// </summary>
    public Task<long> NextIdAsync(CancellationToken cancellationToken) =>
        Task.FromResult(state.NextCategoryId++);

    /// <summary>
    /// Stages a changed account/category in the isolated working copy. Publication to the shared
    /// demo store happens only if the enclosing operation and audit succeed.
    /// </summary>
    public Task SaveAsync(Category category, CancellationToken cancellationToken)
    {
        state.Categories[category.Id] = category;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reads buyer-owned cart selections, saved addresses and benefit context. Cart selection is
    /// product-based under DB-05 B; never assign a fixed batch to CartItem.
    /// </summary>
    public Task<CommerceContext> GetContextAsync(
        long buyerId, CancellationToken cancellationToken) =>
        Task.FromResult(new CommerceContext(
            state.Carts.GetValueOrDefault(buyerId, []).ToArray(),
            state.Addresses.GetValueOrDefault(buyerId, []).ToArray(),
            state.Points.GetValueOrDefault(buyerId),
            state.Vouchers.Values.Where(voucher => voucher.IsActive)
                .Select(voucher => voucher.Code).ToArray()));

    /// <summary>
    /// Reads a current offer with category, seller eligibility and pricing; null denotes a
    /// missing product. This read contract does not implement product management.
    /// </summary>
    public Task<ProductOffer?> FindProductAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Products.GetValueOrDefault(id));

    /// <summary>
    /// Reads candidate batches for one product. Application logic filters sellability and
    /// allocates FEFO; SQL should use the approved batch index and avoid one query per
    /// allocation.
    /// </summary>
    public Task<IReadOnlyList<HarvestBatch>> FindBatchesAsync(
        long productId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HarvestBatch>>(state.Batches.Values
            .Where(batch => batch.ProductId == productId).ToArray());

    /// <summary>
    /// Loads one batch for final reservation revalidation; inventory mutation must remain inside
    /// the shared placement transaction.
    /// </summary>
    public Task<HarvestBatch?> FindBatchAsync(long id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Batches.GetValueOrDefault(id));

    /// <summary>
    /// Reads a voucher and current quota projection. Real quota must include valid
    /// VoucherRedemption reservations, not just UsedCount.
    /// </summary>
    public Task<VoucherOffer?> FindVoucherAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(state.Vouchers.Values.FirstOrDefault(voucher =>
            string.Equals(voucher.Code, code, StringComparison.OrdinalIgnoreCase)));

    /// <summary>
    /// Loads the saved server quote by identity. The caller must bind it to the current buyer
    /// before exposing or using it.
    /// </summary>
    public Task<CheckoutQuote?> FindQuoteAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(state.Quotes.GetValueOrDefault(id));

    /// <summary>
    /// Stages a buyer-bound quote including address, money and FEFO allocation fingerprints.
    /// Preview does not reserve stock; accepted quote retention must support the eventual retry
    /// contract.
    /// </summary>
    public Task SaveQuoteAsync(CheckoutQuote quote, CancellationToken cancellationToken)
    {
        // Bound demo preview history per buyer; orders retain their accepted quote for retries.
        Guid[] removable = state.Quotes.Values.Where(previous => previous.BuyerId == quote.BuyerId
            && !state.Orders.Values.Any(order => order.QuoteId == previous.Id))
            .OrderByDescending(previous => previous.ExpiresAt).Skip(19).Select(q => q.Id).ToArray();
        foreach (Guid id in removable)
        {
            state.Quotes.Remove(id);
        }

        state.Quotes[quote.Id] = quote;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Looks up the buyer-specific DEMO request key. DB-06 is undecided and the current SQL
    /// schema has no request-key column; this contract must be resolved before a durable SQL
    /// adapter is written.
    /// </summary>
    public Task<RetailOrder?> FindOrderAsync(
        long buyerId, Guid requestKey, CancellationToken cancellationToken) =>
        Task.FromResult(state.Orders.Values.SingleOrDefault(order =>
            order.BuyerId == buyerId && order.RequestKey == requestKey));

    /// <summary>
    /// Allocates a local order identity; real persistence must use SQL-generated identity values
    /// in the current transaction.
    /// </summary>
    public Task<long> NextOrderIdAsync(CancellationToken cancellationToken) =>
        Task.FromResult(state.NextOrderId++);

    /// <summary>
    /// Stages immutable order/seller/item and multi-batch allocation snapshots without
    /// committing. It does not initiate payment or implement order tracking.
    /// </summary>
    public Task SaveOrderAsync(RetailOrder order, CancellationToken cancellationToken)
    {
        if (state.Orders.Values.Any(existing => existing.BuyerId == order.BuyerId
            && existing.RequestKey == order.RequestKey))
        {
            throw new InvalidOperationException("Duplicate order inside transaction.");
        }

        state.Orders.Add(order.Id, order);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stages a batch whose available quantity moved into reserved quantity. SQL must use
    /// conditional updates and check affected rows; callers aggregate demand by batch first.
    /// </summary>
    public Task ReserveBatchAsync(HarvestBatch batch, CancellationToken cancellationToken)
    {
        state.Batches[batch.Id] = batch;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Reserves eligible voucher quota and mock available points for this order. Real points
    /// storage is still undecided; payment/expiry owners must supply idempotent apply/release
    /// behavior.
    /// </summary>
    public Task ReserveBenefitsAsync(RetailOrder order, CancellationToken cancellationToken)
    {
        // DEMO-BENEFITS: deduct available fixture points and reserve voucher quota here.
        // The rewards/promotion teams must supply durable reservations keyed by OrderId,
        // with apply/release in their payment/expiry flows. No earn/refund UC is implemented.
        int balance = state.Points.GetValueOrDefault(order.BuyerId);
        if (balance < order.RedeemedPoints)
        {
            throw new InvalidOperationException("Points changed inside transaction.");
        }

        state.Points[order.BuyerId] = balance - order.RedeemedPoints;
        if (order.VoucherId is { } voucherId)
        {
            VoucherOffer voucher = state.Vouchers[voucherId];
            state.Vouchers[voucherId] = voucher with
            {
                ReservedCount = voucher.ReservedCount + 1
            };
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes only the specified buyer-owned cart rows after placement succeeds. Nonselected
    /// rows remain; rollback must restore any staged removals.
    /// </summary>
    public Task RemoveOrderedItemsAsync(
        long buyerId, IReadOnlyList<long> itemIds, CancellationToken cancellationToken)
    {
        state.Carts[buyerId] = state.Carts.GetValueOrDefault(buyerId, [])
            .Where(item => !itemIds.Contains(item.Id)).ToList();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stages the before/after audit beside the business mutation. The fixture is process-local
    /// and is lost on restart, so it is not a durable audit trail.
    /// </summary>
    public Task AddAuditAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        state.Audits.Add(record);
        return Task.CompletedTask;
    }

    #region IAuthRepository
    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        Task.FromResult(state.Accounts.Values.Any(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

    public Task<bool> PhoneExistsAsync(string phone, CancellationToken cancellationToken) =>
        Task.FromResult(state.Accounts.Values.Any(u => string.Equals(u.Phone, phone, StringComparison.OrdinalIgnoreCase)));

    public Task SavePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken)
    {
        state.PendingRegistrations[registration.Email] = registration;
        return Task.CompletedTask;
    }

    public Task<PendingRegistration?> FindPendingRegistrationAsync(string email, CancellationToken cancellationToken)
    {
        state.PendingRegistrations.TryGetValue(email, out var registration);
        return Task.FromResult<PendingRegistration?>(registration);
    }

    public Task UpdatePendingRegistrationAsync(PendingRegistration registration, CancellationToken cancellationToken)
    {
        state.PendingRegistrations[registration.Email] = registration;
        return Task.CompletedTask;
    }

    public Task RemovePendingRegistrationAsync(string email, CancellationToken cancellationToken)
    {
        state.PendingRegistrations.TryRemove(email, out _);
        return Task.CompletedTask;
    }

    public Task<UserAccount> CreateUserAsync(
        string fullName, string phoneNumber, string email, string passwordHash,
        string role, DateTimeOffset createdAt, CancellationToken cancellationToken)
    {
        long newId = state.Accounts.Count == 0 ? 1 : state.Accounts.Keys.Max() + 1;
        var user = new UserAccount
        {
            Id = newId,
            Name = fullName,
            Phone = phoneNumber,
            Email = email,
            Role = role,
            OrganizationName = role == "Farmer" ? fullName : null,
            VerificationStatus = role == "Farmer" ? "NotSubmitted" : null,
            CreatedAt = createdAt
        };
        state.Accounts[newId] = user;
        return Task.FromResult(user);
    }

    public Task CreateFarmerProfileAsync(
        long userId, string farmName, string representativeName, string address,
        string provinceCode, string verificationStatus, decimal reputationScore,
        CancellationToken cancellationToken)
    {
        if (state.Accounts.TryGetValue(userId, out var account))
        {
            var updated = account.Copy();
            state.Accounts[userId] = updated;
        }
        return Task.CompletedTask;
    }
    #endregion

    #region IProfileRepository
    public Task<UserProfileResponse?> GetProfileAsync(long userId, CancellationToken cancellationToken)
    {
        if (!state.Accounts.TryGetValue(userId, out var user))
        {
            return Task.FromResult<UserProfileResponse?>(null);
        }

        var addresses = state.Addresses.GetValueOrDefault(userId, []);
        var firstAddr = addresses.FirstOrDefault();
        DeliveryAddressDto? addrDto = firstAddr == null ? null : new DeliveryAddressDto(
            1, firstAddr.RecipientName, firstAddr.Phone, firstAddr.Province, null,
            firstAddr.Ward, firstAddr.Detail, firstAddr.ProvinceCode, firstAddr.WardCode, true);

        FarmerProfileDto? farmerDto = user.Role != "Farmer" ? null : new FarmerProfileDto(
            userId + 1000, user.OrganizationName ?? user.Name, user.Name, "5.0 ha", null,
            "Lâm Đồng", null, "BL-2026", "Địa chỉ nông trại demo", "01",
            user.VerificationStatus ?? "Approved", 5.0m, null, null);

        int rewardPoints = state.Points.GetValueOrDefault(userId, 500);

        return Task.FromResult<UserProfileResponse?>(new UserProfileResponse(
            user.Id, user.Name, user.Phone, user.Email, user.Role, user.Status,
            null, null, null, rewardPoints, addrDto, farmerDto));
    }

    public Task UpdateConsumerProfileAsync(
        long userId, UpdateConsumerProfileRequest request, CancellationToken cancellationToken)
    {
        if (state.Accounts.TryGetValue(userId, out var user))
        {
            var updated = new UserAccount
            {
                Id = user.Id,
                Name = request.FullName.Trim(),
                Phone = request.PhoneNumber.Trim(),
                Email = user.Email,
                Role = user.Role,
                OrganizationName = user.OrganizationName,
                VerificationStatus = user.VerificationStatus,
                CreatedAt = user.CreatedAt
            };
            state.Accounts[userId] = updated;

            if (!string.IsNullOrWhiteSpace(request.AddressDetail))
            {
                state.Addresses[userId] = [new AddressSnapshot(
                    request.FullName, request.PhoneNumber, request.ProvinceCity ?? "Hà Nội",
                    request.Ward ?? "Dịch Vọng Hậu", request.AddressDetail,
                    request.ProvinceCode ?? "01", request.WardCode ?? "001")];
            }
        }
        return Task.CompletedTask;
    }

    public Task UpdateFarmerProfileAsync(
        long userId, UpdateFarmerProfileRequest request, bool requiresReverification, CancellationToken cancellationToken)
    {
        if (state.Accounts.TryGetValue(userId, out var user))
        {
            var updated = new UserAccount
            {
                Id = user.Id,
                Name = request.FullName.Trim(),
                Phone = request.PhoneNumber.Trim(),
                Email = user.Email,
                Role = user.Role,
                OrganizationName = request.FarmName.Trim(),
                VerificationStatus = requiresReverification ? "PendingApproval" : user.VerificationStatus,
                CreatedAt = user.CreatedAt
            };
            state.Accounts[userId] = updated;
        }
        return Task.CompletedTask;
    }

    public Task SubmitFarmerKycAsync(long userId, string? notes, CancellationToken cancellationToken)
    {
        if (state.Accounts.TryGetValue(userId, out var user))
        {
            var updated = new UserAccount
            {
                Id = user.Id,
                Name = user.Name,
                Phone = user.Phone,
                Email = user.Email,
                Role = user.Role,
                OrganizationName = user.OrganizationName,
                VerificationStatus = "PendingApproval",
                CreatedAt = user.CreatedAt
            };
            state.Accounts[userId] = updated;
        }
        return Task.CompletedTask;
    }
    #endregion

    #region IOrderTrackingRepository
    public Task<PagedResponse<OrderSummaryDto>> GetOrdersByBuyerAsync(
        long buyerId, OrderQuery query, CancellationToken cancellationToken)
    {
        var orders = state.Orders.Values
            .Where(o => o.BuyerId == buyerId);

        if (!string.IsNullOrWhiteSpace(query.Status) && query.Status != "All")
        {
            orders = orders.Where(o => o.Status == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            string kw = query.Keyword.Trim();
            orders = orders.Where(o => o.Code.Contains(kw, StringComparison.OrdinalIgnoreCase));
        }

        var list = orders.OrderByDescending(o => o.CreatedAt).ToList();
        var paged = list
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => MapDemoOrderSummary(o))
            .ToList();

        return Task.FromResult(new PagedResponse<OrderSummaryDto>(
            paged, list.Count, query.Page, query.PageSize));
    }

    public Task<OrderDetailDto?> GetOrderDetailAsync(
        long orderId, long? buyerId, CancellationToken cancellationToken)
    {
        var order = state.Orders.Values.FirstOrDefault(o => o.Id == orderId);
        if (order == null || (buyerId.HasValue && order.BuyerId != buyerId.Value))
        {
            return Task.FromResult<OrderDetailDto?>(null);
        }

        var summary = MapDemoOrderSummary(order);
        var timeline = new List<OrderStatusTimelineDto>
        {
            new("Pending", summary.OrderDate, "Đã tạo đơn hàng"),
            new(summary.OrderStatus, summary.OrderDate.AddHours(2), "Cập nhật trạng thái đơn hàng")
        };

        return Task.FromResult<OrderDetailDto?>(new OrderDetailDto(
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
            summary.OrderStatus == "Cancelled" ? "Refunded" : "Paid",
            summary.OrderDate.AddDays(2),
            summary.SellerOrders,
            timeline));
    }

    public Task<OrderDetailDto?> GetOrderDetailByCodeAsync(
        string orderCode, long? buyerId, CancellationToken cancellationToken)
    {
        var order = state.Orders.Values.FirstOrDefault(o =>
            string.Equals(o.Code, orderCode, StringComparison.OrdinalIgnoreCase));
        if (order == null) return Task.FromResult<OrderDetailDto?>(null);
        return GetOrderDetailAsync(order.Id, buyerId, cancellationToken);
    }

    public Task MarkOrderNotificationsReadAsync(
        long orderId, long buyerId, CancellationToken cancellationToken) => Task.CompletedTask;

    private static OrderSummaryDto MapDemoOrderSummary(RetailOrder o)
    {
        var sellerGroups = o.Sellers.Select(s =>
        {
            string sellerName = o.Lines.FirstOrDefault(l => l.SellerId == s.SellerId)?.SellerName ?? "HTX Nông Nghiệp";
            return new SellerOrderGroupDto(
                s.SellerId,
                s.SellerId,
                sellerName,
                "Lâm Đồng",
                s.Code,
                o.Status,
                s.FinalAmount,
                o.Lines.Where(l => l.SellerId == s.SellerId).Select(l => new OrderItemDetailDto(
                    l.ProductId,
                    l.ProductId,
                    l.ProductName,
                    l.Quantity,
                    l.Unit,
                    l.UnitPrice,
                    l.Subtotal,
                    l.Allocations.FirstOrDefault()?.BatchId.ToString() ?? "LOT-DEMO",
                    DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
                    "Vườn A",
                    false,
                    "🥬")).ToList());
        }).ToList();

        return new OrderSummaryDto(
            o.Id,
            o.Code,
            o.CreatedAt,
            o.Status,
            o.PaymentMethod,
            o.DeliveryMethod,
            o.Subtotal,
            o.DeliveryFee,
            o.VoucherDiscount + o.PointsDiscount,
            o.Total,
            o.Address.RecipientName,
            o.Address.Phone,
            $"{o.Address.Detail}, {o.Address.Ward}, {o.Address.Province}",
            "AGRI-EXPRESS-VN01",
            sellerGroups);
    }
    #endregion
}
