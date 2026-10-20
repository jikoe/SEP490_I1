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
public sealed class ProductServiceTests
{
    public static IEnumerable<object[]> ProductCases => Enumerable.Range(1, 21).Select(n => new object[] {$"UT-FAR-03-{n:000}", n});
    internal static ImageInput Image(int size = 16) { var bytes = new byte[size]; new byte[] {137,80,78,71,13,10,26,10}.CopyTo(bytes, 0); return new("image.png", "image/png", bytes, true); }
    internal static ProductInput Input() => new("Tomato", 1, "kg", "Đồng Tháp", "Tomato description", 30000, null, null, [Image()], RequestKey: Guid.NewGuid());
    private static void Code<T>(Result<T> result, string code) { Assert.False(result.IsSuccess); Assert.Equal(code, result.Error!.Code); }

    [Theory, MemberData(nameof(ProductCases))]
    public async Task Product_design(string caseId, int n)
    {
        Assert.StartsWith("UT-FAR-03", caseId);
        var variants = n switch {3 => 2, 4 => 5, 5 => 6, 7 => 2, 19 => 2, 21 => 3, _ => 1};
        for (int variant = 0; variant < variants; variant++)
        {
            var r = new FakeRepository(); var service = new ProductService(r, new FakeUser(1)); var input = Input();
            Result<ActionResult> result;
            if (n <= 10)
            {
                if (n == 2) input = input with { WholesaleMinimum = 2, WholesalePrice = 30000 };
                if (n == 3) r.Actor = r.Actor with {FarmApproved = false};
                if (n == 4) input = variant switch {0 => input with {Name = ""}, 1 => input with {CategoryId = 0}, 2 => input with {Unit = ""}, 3 => input with {Origin = ""}, _ => input with {Description = ""}};
                if (n == 5) input = variant switch {0 => input with {RetailPrice = 0}, 1 => input with {RetailPrice = -1}, 2 => input with {WholesaleMinimum = 1, WholesalePrice = 20}, 3 => input with {WholesaleMinimum = 2, WholesalePrice = 0}, 4 => input with {WholesaleMinimum = 2, WholesalePrice = 40000}, _ => input with {WholesaleMinimum = 2}};
                if (n == 6) input = input with {Images = [Image() with {Primary = false}]};
                if (n == 7) input = input with {Images = [Image() with {Name = variant == 0 ? "image.gif" : "image.pdf", ContentType = variant == 0 ? "image/gif" : "application/pdf"}]};
                if (n == 8) input = input with {Images = [Image(5 * 1024 * 1024)]};
                if (n == 9) input = input with {Images = [Image(5 * 1024 * 1024 + 1)]};
                if (n == 10) { r.Product.Approval = "Rejected"; r.Product.RejectionReason = "Origin missing"; input = input with {ProductId = 1}; }
                result = await service.SubmitAsync(input);
                string? error = n switch {3 => "MSG75", 4 => "MSG120", 5 => "MSG77", 6 => "MSG76", 7 or 9 => "MSG78", _ => null};
                if (error is not null) { Code(result, error); Assert.Equal(0, r.ListingSaves); Assert.Equal(0, r.Commits); }
                else { Assert.True(result.IsSuccess); Assert.Equal("MSG71", result.Value!.MessageCode); Assert.False(r.Product.Published); Assert.Equal("Pending", r.Product.Approval); Assert.Equal(1, r.ListingSaves); Assert.Equal(1, r.Commits); if (n == 10) { Assert.Equal(1, result.Value.Id); Assert.Equal("Origin missing", r.Product.RejectionReason); } }
            }
            else if (n <= 16)
            {
                input = input with { Images = [new("", "", null, true, "/image/1")], WholesaleMinimum = 10, WholesalePrice = 25000 };
                if (n == 11) input = input with {Name = "New tomato"};
                if (n == 12) input = input with {RetailPrice = 32000};
                if (n == 13) { r.Product.UnitLocked = true; input = input with {Unit = "bag"}; }
                if (n == 14) input = input with {Unit = "bag"};
                if (n == 15) r.Product.FarmId = 99;
                if (n == 16) input = input with {WholesalePrice = 40000};
                result = await service.UpdateAsync(1, input);
                string? error = n switch {13 => "MSG79", 15 => "MSG80", 16 => "MSG77", _ => null};
                if (error is not null) { Code(result, error); Assert.Equal(0, r.ListingSaves); }
                else { Assert.True(result.IsSuccess); Assert.Equal("MSG72", result.Value!.MessageCode); Assert.Equal(n == 12, r.Product.Published); Assert.Equal(n == 12 ? "Approved" : "Pending", r.Product.Approval); Assert.Equal(1, r.Commits); }
            }
            else
            {
                if (n == 18) r.Product.Published = false;
                if (n == 19) { r.Product.Approval = variant == 0 ? "Pending" : "Rejected"; r.Product.Published = false; }
                if (n == 20) r.Actor = r.Actor with {FarmApproved = false};
                if (n == 21) { if (variant == 0) r.Product.CategoryActive = false; if (variant == 1) r.Product.RetailPrice = 0; if (variant == 2) r.Product.Images = []; }
                result = await service.SetVisibilityAsync(1, n != 17, 0, true);
                if (n >= 19) { Code(result, n == 19 ? "MSG131" : n == 20 ? "MSG75" : "MSG120"); Assert.Equal(0, r.ListingSaves); }
                else { Assert.True(result.IsSuccess); Assert.Equal(n == 17 ? "MSG74" : "MSG164", result.Value!.MessageCode); Assert.Equal(n != 17, r.Product.Published); Assert.Equal("Approved", r.Product.Approval); Assert.Equal(1, r.Commits); }
            }
        }
    }
    [Fact]
    public async Task Successful_new_listing_retry_reuses_durable_request_key()
    {
        var repo = new FakeRepository(); var service = new ProductService(repo, new FakeUser(1));
        var input = Input();
        var first = await service.SubmitAsync(input); var repeated = await service.SubmitAsync(input);
        Assert.True(first.IsSuccess); Assert.Equal(first.Value!.Id, repeated.Value!.Id);
        Assert.Equal(1, repo.ListingSaves);
    }

    [Fact]
    public async Task Price_only_update_does_not_reset_approval_but_stale_revision_is_rejected()
    {
        var repo = new FakeRepository(); var service = new ProductService(repo, new FakeUser(1));
        var input = Input() with {RetailPrice = 31000, WholesaleMinimum = 10, WholesalePrice = 25000,
            Images = [new("", "", null, true, "/image/1")]};
        Assert.True((await service.UpdateAsync(1, input)).IsSuccess);
        Assert.Equal("MSG122", (await service.UpdateAsync(1, input)).Error!.Code);
        Assert.True(repo.Product.Published); Assert.Equal("Approved", repo.Product.Approval);
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
