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

namespace AgriSustain.UnitTests.RetailCustomer;

// Real application services; all data, identity, clock and persistence fakes are local to this file.
// No DbContext, SQL connection, HTTP client, seed script or external fixture is used.
public sealed class CartServiceTests
{
    public static IEnumerable<object[]> CartCases => Enumerable.Range(1, 22).Select(n => new object[] {$"UT-RC-01-{n:000}", n});
    private static void Code<T>(Result<T> result, string code) { Assert.False(result.IsSuccess); Assert.Equal(code, result.Error!.Code); }

    [Theory, MemberData(nameof(CartCases))]
    public async Task Cart_design(string caseId, int n)
    {
        Assert.StartsWith("UT-RC-01", caseId);
        foreach (var invalidQuantity in n == 10 ? new[] {0m, -1, 1.5m} : new[] {1m})
        {
            var r = new FakeRepository(); r.Retail(); var clock = new FakeClock(); var service = new CartService(r, new FakeUser(2), clock);
            Result<CartResponse> result;
            if (n <= 5)
            {
                if (n is 2 or 3 or 4) r.Lines = [r.Line(qty: n == 4 ? 4 : 2)];
                if (n == 4) { r.Product.Batches = [FakeRepository.Batch() with {Available = 5}]; r.Lines[0].Offer = FakeRepository.Copy(r.Product); }
                if (n == 5) r.Product.Batches = [FakeRepository.Batch() with {Available = 0}];
                result = await service.AddItemAsync(new(1, n == 3 ? 21 : 20, n == 4 ? 2 : n == 1 || n == 5 ? 2 : 3));
                if (n == 5) { Code(result, "MSG88"); Assert.Empty(r.Lines); }
                else { Assert.True(result.IsSuccess); Assert.Equal(n == 3 ? 2 : 1, result.Value!.Items.Length); Assert.Equal(n == 4 ? "MSG86" : "MSG81", result.Value.MessageCode); Assert.Equal(n == 1 ? 2 : n == 3 ? 2 : 5, result.Value.Items[0].Quantity); Assert.Equal(n == 1 ? 60000 : n == 3 ? 150000 : 150000, result.Value.SelectedSubtotal); }
            }
            else if (n <= 12)
            {
                r.Lines = [r.Line(qty: n == 6 ? 10 : 3)];
                if (n == 9) { r.Lines[0].Offer!.WholesaleMinimum = null; r.Lines[0].Offer!.WholesalePrice = null; }
                if (n is 11 or 12) r.Lines[0].Offer!.Batches = [FakeRepository.Batch() with {Available = n == 11 ? 2.4m : .5m}];
                decimal qty = n switch {6 => 9, 7 => 10, 8 => 11, 9 => 10, 10 => invalidQuantity, 11 => 3, _ => 1};
                result = await service.UpdateQuantityAsync(1, qty);
                if (n == 10) { Code(result, "MSG167"); Assert.Equal(3, r.Lines[0].Quantity); Assert.Equal(0, r.CartSaves); }
                else { Assert.True(result.IsSuccess); var l = result.Value!.Items[0]; Assert.Equal(n == 11 ? 2 : n == 12 ? 1 : qty, l.Quantity); Assert.Equal(n is 7 or 8 ? 25000 : 30000, l.UnitPrice); if (n == 12) { Assert.False(l.Selected); Assert.False(l.Available); } if (n == 6) Assert.Equal("MSG83", result.Value.MessageCode); if (n == 7) Assert.Equal("MSG82", result.Value.MessageCode); }
            }
            else if (n <= 16)
            {
                r.Lines = [r.Line(), r.Line(2, 1, false)];
                if (n == 15) r.Lines[0].Offer!.Batches = [FakeRepository.Batch() with {Available = 0}];
                result = await service.SetSelectionAsync(new(n == 16 ? [99] : n == 15 ? [1, 2] : [1], n != 14));
                if (n == 16) { Code(result, "MSG121"); Assert.Equal(0, r.CartSaves); }
                else { Assert.True(result.IsSuccess); Assert.Equal(n == 13 ? 60000 : n == 14 ? 0 : 30000, result.Value!.SelectedSubtotal); if (n is 14 or 15) Assert.False(result.Value.Items[0].Selected); }
            }
            else
            {
                r.Lines = [r.Line(1, n == 19 ? 5 : 2), r.Line(2, 1, false)];
                if (n == 18) foreach (var l in r.Lines) l.Selected = false;
                string token = (await service.GetAsync()).Value!.ReviewToken;
                if (n == 19) r.Lines[0].Offer!.Batches = [FakeRepository.Batch() with {Available = 3}];
                if (n == 20) { r.Lines[0].Offer!.Batches = [FakeRepository.Batch() with {Status = "Recalled"}]; r.Lines[1].Selected = true; }
                if (n == 21) r.Lines[0].Offer!.RetailPrice = 32000;
                if (n == 22) { r.Lines[0].Selected = false; r.Lines[0].Offer!.Batches = [FakeRepository.Batch() with {Available = 0}]; r.Lines[1].Selected = true; token = (await service.GetAsync()).Value!.ReviewToken; }
                result = await service.PrepareCheckoutAsync(token);
                Assert.True(result.IsSuccess);
                Assert.Equal(n is 17 or 22, result.Value!.CanProceedToCheckout);
                if (n == 18) Assert.Equal("MSG87", result.Value.MessageCode);
                if (n == 19) { Assert.Equal("MSG86", result.Value.MessageCode); Assert.Equal(3, result.Value.Items[0].Quantity); }
                if (n == 20) { Assert.Equal("MSG88", result.Value.MessageCode); Assert.False(result.Value.Items[0].Selected); }
                if (n == 21) { Assert.Equal("MSG118", result.Value.MessageCode); Assert.Equal(32000, result.Value.Items[0].UnitPrice); }
                if (n is 19 or 20 or 21) Assert.True((await service.PrepareCheckoutAsync(result.Value.ReviewToken)).Value!.CanProceedToCheckout);
                Assert.Equal(0, r.OrderSaves);
            }
            Assert.Equal(0, r.OrderSaves); Assert.Equal(0, r.ListingSaves);
        }
    }
    [Fact]
    public async Task Checkout_review_treats_equivalent_decimal_scales_as_equal()
    {
        var repo = new FakeRepository(); repo.Retail(repo.Line());
        var service = new CartService(repo, new FakeUser(2), new FakeClock());
        var shown = (await service.GetAsync()).Value!;
        repo.Lines[0].Quantity = 2.000m;
        repo.Lines[0].Offer!.RetailPrice = 30000.00m;
        repo.Lines[0].Offer!.WholesaleMinimum = 10.000m;
        repo.Lines[0].Offer!.QuantityStep = 1.000m;
        Assert.True((await service.PrepareCheckoutAsync(shown.ReviewToken)).Value!.CanProceedToCheckout);
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
