using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Commerce;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;

namespace AgriSustain.UnitTests;

internal sealed class CommerceTestClock : TimeProvider
{
    public DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T03:00:00Z");
    public override DateTimeOffset GetUtcNow() => Now;
}
internal sealed class CommerceUser(long id) : ICurrentUser { public long? UserId => id; }
internal sealed class CommerceDouble : ICommerceUnitOfWork, ICommerceRepository
{
    public CommerceActor Actor = new(1, "Farmer", "Active", 10, true);
    public Listing Product = Offer();
    public BasketLine[] Lines = [];
    public FulfillmentOrder Order = new() { Id = 100, FarmId = 10, BuyerId = 2, Status = "PendingConfirmation", RequiredPayment = 100,
        VerifiedPayment = 100, Lines = [new(50, 1, "Tomato", "kg", 5, 30000, [new(9, 5, "Reserved", Batch() with { Reserved = 5 })])] };
    public bool CategoryActive = true, ThrowOnCommit;
    public int ListingSaves, CartSaves, OrderSaves, Commits;
    public string? LastAction;
    public Dictionary<Guid,long> Submissions = [];
    public Task<long?> SubmissionAsync(long actorId, Guid key) => Task.FromResult(Submissions.TryGetValue(key, out long id) ? (long?)id : null);
    public Task RecordSubmissionAsync(long actorId, Guid key, long productId) { Submissions[key] = productId; return Task.CompletedTask; }
    public static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    public static BatchOffer Batch(long id = 20) => new(id, 1, "B" + id, 20, 0, "Available", new(2026,10,18), new(2026,11,1), true);
    public static Listing Offer() => new() { Id = 1, FarmId = 10, FarmName = "Farm1", Name = "Tomato", CategoryId = 1, CategoryActive = true,
        Unit = "kg", Origin = "Đồng Tháp", Description = "Tomato description", RetailPrice = 30000, WholesaleMinimum = 10,
        WholesalePrice = 25000, Approval = "Approved", Published = true, FarmEligible = true, Images = [new("/image/1", true)],
        Batches = [Batch(), Batch(21)] };
    public void Retail(params BasketLine[] lines) { Actor = new(2, "RetailCustomer", "Active", null, false); Lines = lines; }
    public BasketLine Line(long id = 1, decimal qty = 2, bool selected = true, long batch = 20) => new() {Id = id, ProductId = 1, BatchId = batch, Quantity = qty, Selected = selected, Offer = Copy(Product)};
    public async Task<Result<T>> RunAsync<T>(Func<ICommerceRepository, Task<Result<T>>> work, CancellationToken cancellationToken = default, bool readOnly = false)
    {
        // Service tests assert collaborators; durable rollback belongs to SQL IT.
        var result = await work(this);
        if (result.IsSuccess && ThrowOnCommit) return Result<T>.Failure(new Error("MSG123", "Injected commit failure"));
        if (result.IsSuccess && ListingSaves + CartSaves + OrderSaves > 0) Commits++;
        return result;
    }
    public Task<CommerceActor?> ActorAsync(long id) => Task.FromResult<CommerceActor?>(Actor.Id == id ? Actor : null);
    public Task<CategoryOption[]> CategoriesAsync() => Task.FromResult(new[] {new CategoryOption(1, "Fruit")});
    public Task<bool> CategoryActiveAsync(long id) => Task.FromResult(CategoryActive && id == 1);
    public Task<PagedResponse<Listing>> ListingsAsync(long? farmId, CommerceQuery q) => Task.FromResult(new PagedResponse<Listing>([Copy(Product)], 1, q.Page, q.PageSize));
    public Task<Listing?> ListingAsync(long id) => Task.FromResult<Listing?>(id == Product.Id ? Copy(Product) : null);
    public Task<long> SaveListingAsync(Listing p, ImageInput[]? images, long actorId, string action) { ListingSaves++; Product = Copy(p); LastAction = action; return Task.FromResult(p.Id == 0 ? 11L : p.Id); }
    public Task<BasketLine[]> CartAsync(long userId) => Task.FromResult(Copy(Lines));
    public Task SaveCartAsync(long userId, BasketLine[] lines) { CartSaves++; long next = 1; foreach (var l in lines.Where(l => l.Id == 0)) l.Id = ++next; Lines = Copy(lines); return Task.CompletedTask; }
    public Task<PagedResponse<FulfillmentOrder>> OrdersAsync(long actorId, bool buyer, CommerceQuery q) => Task.FromResult(new PagedResponse<FulfillmentOrder>([Copy(Order)], 1, q.Page, q.PageSize));
    public Task<FulfillmentOrder?> OrderAsync(long id, long actorId, bool buyer) => Task.FromResult<FulfillmentOrder?>(id == Order.Id && (buyer ? Order.BuyerId == actorId : Order.FarmId == Actor.FarmId) ? Copy(Order) : null);
    public Task SaveOrderAsync(FulfillmentOrder order, long actorId, string action, string? reason) { OrderSaves++; Order = Copy(order); LastAction = action; return Task.CompletedTask; }
    public Task SaveEvidenceAsync(FulfillmentOrder order, long actorId, EvidenceInput input, bool failure) { OrderSaves++; return Task.CompletedTask; }
}
