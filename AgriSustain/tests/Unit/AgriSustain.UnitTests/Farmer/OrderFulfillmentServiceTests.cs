using AgriSustain.Application.Interfaces.UnitOfWork;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Services.Shared;
using AgriSustain.Application.Services.Farmer;
using AgriSustain.Application.Services.RetailCustomer;
using Xunit;

namespace AgriSustain.UnitTests.Farmer;

// Real application services; all data, identity, clock and persistence fakes are local to this file.
// No DbContext, SQL connection, HTTP client, seed script or external fixture is used.
public sealed class OrderFulfillmentServiceTests
{
    public static IEnumerable<object[]> OrderCases => Enumerable.Range(1, 16).Select(n => new object[] {$"UT-FAR-06-{n:000}", n});
    private static void Code<T>(Result<T> result, string code) { Assert.False(result.IsSuccess); Assert.Equal(code, result.Error!.Code); }

    [Theory, MemberData(nameof(OrderCases))]
    public async Task Fulfillment_design(string caseId, int n)
    {
        Assert.StartsWith("UT-FAR-06", caseId);
        int variants = n == 5 || n == 8 ? 3 : 1;
        for (int v = 0; v < variants; v++)
        {
            var r = new FakeRepository(); var service = new OrderFulfillmentService(r, new FakeUser(1), new FakeClock());
            if (n == 2) r.Order.VerifiedPayment = 0;
            if (n == 3) { r.Order.PreOrder = true; r.Order.RequiredPayment = 20; r.Order.VerifiedPayment = 20; }
            if (n == 4) { r.Order.PreOrder = true; r.Order.Lines = [r.Order.Lines[0] with {Allocations = []}]; }
            if (n == 5) r.Order.Lines = [r.Order.Lines[0] with {Allocations = [r.Order.Lines[0].Allocations[0] with {Batch = FakeRepository.Batch() with {Status = v == 0 ? "Recalled" : v == 1 ? "Expired" : "Blocked"}}]}];
            if (n == 6) r.Order.Status = "Confirmed";
            if (n is >= 7 and <= 10) r.Order.Status = n == 8 ? new[] {"Pending", "Cancelled", "Shipping"}[v] : n == 10 ? "ReadyToShip" : "Confirmed";
            if (n == 9) r.Order.FarmId = 99;
            if (n >= 11) r.Order.Status = "ReadyToShip";
            if (n == 12) r.Order.Lines = [r.Order.Lines[0] with {Allocations = []}];
            if (n == 13) r.Order.Lines = [r.Order.Lines[0] with {Allocations = [r.Order.Lines[0].Allocations[0] with {Batch = FakeRepository.Batch() with {Status = "Recalled"}}]}];
            if (n == 14) r.Order.Status = "Pending";
            if (n == 15) r.Order.Status = "Shipping";
            if (n == 16) r.ThrowOnCommit = true;
            var result = n <= 6 ? await service.ConfirmAsync(100) : n <= 10 ? await service.MarkReadyAsync(100) : await service.DispatchAsync(100);
            string? expected = n switch {2 or 4 or 12 => "MSG148", 5 or 13 => "MSG139", 6 or 10 or 15 => "MSG122", 8 or 14 => "MSG131", 9 => "MSG121", 16 => "MSG123", _ => null};
            if (expected is not null) { Code(result, expected); Assert.Equal(0, r.Commits); if (n != 16) Assert.Equal(0, r.OrderSaves); }
            else { Assert.True(result.IsSuccess); Assert.Equal(n <= 6 ? "MSG64" : n <= 10 ? "MSG146" : "MSG66", result.Value!.MessageCode); Assert.Equal(1, r.OrderSaves); Assert.Equal(1, r.Commits); Assert.Equal(n <= 6 ? "Confirm" : n <= 10 ? "Ready" : "Dispatch", r.LastAction); }
        }
    }
    [Fact]
    public async Task Receive_then_accept_keeps_original_delivery_time()
    {
        var repo = new FakeRepository(); repo.Retail(); repo.Order.Status = "Shipping";
        var clock = new FakeClock(); var service = new OrderFulfillmentService(repo, new FakeUser(2), clock);
        Assert.True((await service.ReceiveAsync(100)).IsSuccess); var original = repo.Order.DeliveredAt;
        clock.Now = clock.Now.AddHours(3); Assert.True((await service.ReceiveAsync(100, true)).IsSuccess);
        Assert.Equal(original, repo.Order.DeliveredAt); Assert.True(repo.Order.ReceivedAt > original);
    }
    private sealed class FakeClock : TimeProvider
    {
        public DateTimeOffset Now = DateTimeOffset.Parse("2026-10-19T03:00:00Z");
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class FakeUser(long id) : ICurrentUser { public long? UserId => id; }
    private sealed class FakeRepository : IFarmerRetailUnitOfWork, IFarmerRetailRepository
    {
        public FarmerRetailActor Actor = new(1, "Farmer", "Active", 10, true);
        public Listing Product = Offer();
        public BasketLine[] Lines = [];
        public FulfillmentOrder Order = new() { Id = 100, FarmId = 10, BuyerId = 2, Status = "PendingConfirmation", RequiredPayment = 100,
            VerifiedPayment = 100, Lines = [new(50, 1, "Tomato", "kg", 5, 30000, [new(9, 5, "Reserved", Batch() with { Reserved = 5 })])] };
        public bool CategoryActive = true;
        public bool ThrowOnCommit { get; set; }
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
        public async Task<Result<T>> RunAsync<T>(Func<IFarmerRetailRepository, Task<Result<T>>> work, CancellationToken cancellationToken = default, bool readOnly = false)
        {
            // Service tests assert collaborators; durable rollback belongs to SQL IT.
            var result = await work(this);
            if (result.IsSuccess && ThrowOnCommit) return Result<T>.Failure(new Error("MSG123", "Injected commit failure"));
            if (result.IsSuccess && ListingSaves + CartSaves + OrderSaves > 0) Commits++;
            return result;
        }
        public Task<FarmerRetailActor?> ActorAsync(long id) => Task.FromResult<FarmerRetailActor?>(Actor.Id == id ? Actor : null);
        public Task<CategoryOption[]> CategoriesAsync() => Task.FromResult(new[] {new CategoryOption(1, "Fruit")});
        public Task<bool> CategoryActiveAsync(long id) => Task.FromResult(CategoryActive && id == 1);
        public Task<PagedResponse<Listing>> ListingsAsync(long? farmId, FarmerRetailQuery q) => Task.FromResult(new PagedResponse<Listing>([Copy(Product)], 1, q.Page, q.PageSize));
        public Task<Listing?> ListingAsync(long id) => Task.FromResult<Listing?>(id == Product.Id ? Copy(Product) : null);
        public Task<long> SaveListingAsync(Listing p, ImageInput[]? images, long actorId, string action) { ListingSaves++; Product = Copy(p); LastAction = action; return Task.FromResult(p.Id == 0 ? 11L : p.Id); }
        public Task<BasketLine[]> CartAsync(long userId) => Task.FromResult(Copy(Lines));
        public Task SaveCartAsync(long userId, BasketLine[] lines) { CartSaves++; long next = 1; foreach (var l in lines.Where(l => l.Id == 0)) l.Id = ++next; Lines = Copy(lines); return Task.CompletedTask; }
        public Task<PagedResponse<FulfillmentOrder>> OrdersAsync(long actorId, bool buyer, FarmerRetailQuery q) => Task.FromResult(new PagedResponse<FulfillmentOrder>([Copy(Order)], 1, q.Page, q.PageSize));
        public Task<FulfillmentOrder?> OrderAsync(long id, long actorId, bool buyer) => Task.FromResult<FulfillmentOrder?>(id == Order.Id && (buyer ? Order.BuyerId == actorId : Order.FarmId == Actor.FarmId) ? Copy(Order) : null);
        public Task SaveOrderAsync(FulfillmentOrder order, long actorId, string action, string? reason) { OrderSaves++; Order = Copy(order); LastAction = action; return Task.CompletedTask; }
        public Task SaveEvidenceAsync(FulfillmentOrder order, long actorId, EvidenceInput input, bool failure) { OrderSaves++; return Task.CompletedTask; }
    }
}
