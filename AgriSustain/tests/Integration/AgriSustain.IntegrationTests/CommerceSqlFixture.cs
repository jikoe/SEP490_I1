using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.DTOs.Commerce;
using Microsoft.Extensions.DependencyInjection;
using AgriSustain.Infrastructure.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AgriSustain.IntegrationTests;

internal sealed class SqlTheoryAttribute : TheoryAttribute
{
    public SqlTheoryAttribute() { if (Environment.GetEnvironmentVariable("THREE_UC_SQL_TESTS") != "1") Skip = "Requires disposable SQL Server. Set THREE_UC_SQL_TESTS=1."; }
}
internal sealed class CommerceSqlFixture : IAsyncDisposable
{
    public SqlTestDatabase Database = null!;
    public SqlApiFactory Host = null!;
    public HttpClient Farmer = null!, Buyer = null!, Other = null!;
    public long ProductId, BatchId, Batch2Id, CategoryId, FarmerId, BuyerId;
    public static async Task<CommerceSqlFixture> Create()
    {
        var f = new CommerceSqlFixture {Database = await SqlTestDatabase.CreateAsync()};
        try
        {
            await f.Database.ExecuteAsync("""
                UPDATE dbo.Product SET WholesaleMinQuantity=10,WholesalePrice=25000,OriginProvince=N'Đồng Tháp' WHERE Description='DEMO-THREE-UC:greens';
                UPDATE dbo.HarvestBatch SET ActualHarvestDate='2026-10-18',ExpirationDate='2099-01-01',StockEnteredAt=SYSUTCDATETIME(),AvailableQuantity=20,ReservedQuantity=0;
                INSERT dbo.ProductImage(ProductId,ImageUrl,DisplayOrder,IsPrimary) SELECT ProductId,'/test-image.png',0,1 FROM dbo.Product p WHERE NOT EXISTS(SELECT 1 FROM dbo.ProductImage i WHERE i.ProductId=p.ProductId);
                DELETE FROM dbo.CartItem;
                """);
            f.ProductId = await f.Number("SELECT ProductId FROM dbo.Product WHERE Description='DEMO-THREE-UC:greens'");
            f.BatchId = await f.Number("SELECT HarvestBatchId FROM dbo.HarvestBatch WHERE BatchCode='DEMO3UC-RAU-FIRST'");
            f.Batch2Id = await f.Number("SELECT HarvestBatchId FROM dbo.HarvestBatch WHERE BatchCode='DEMO3UC-RAU-SECOND'");
            f.CategoryId = await f.Number($"SELECT CategoryId FROM dbo.Product WHERE ProductId={f.ProductId}");
            f.FarmerId = await f.Number("SELECT UserId FROM dbo.[User] WHERE Username='demo3uc_farmer_green'");
            f.BuyerId = await f.Number("SELECT UserId FROM dbo.[User] WHERE Username='demo3uc_retail'");
            f.Host = new SqlApiFactory(f.Database.ConnectionString);
            f.Farmer = f.Host.Client("demo3uc_farmer_green"); f.Buyer = f.Host.Client("demo3uc_retail"); f.Other = f.Host.Client("demo3uc_retail_other");
            return f;
        }
        catch { await f.Database.DisposeAsync(); throw; }
    }
    public Task<long> Number(string sql) => Database.NumberAsync(sql);
    public Task Sql(string sql) => Database.ExecuteAsync(sql);
    public async Task<T> Ok<T>(HttpResponseMessage response)
    {
        Assert.True(response.IsSuccessStatusCode, response.StatusCode + ": " + await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<T>())!;
    }
    public async Task Code(HttpResponseMessage response, string code)
    {
        Assert.False(response.IsSuccessStatusCode);
        Assert.Contains('"' + code + '"', await response.Content.ReadAsStringAsync());
    }
    public async Task<CartResponse> Add(decimal qty = 2, long? batch = null) => await Ok<CartResponse>(await Buyer.PostAsJsonAsync("/api/cart/items", new AddCartItem(ProductId, batch ?? BatchId, qty)));
    public async Task<CartResponse> Cart() => (await Buyer.GetFromJsonAsync<CartResponse>("/api/cart"))!;
    public async Task<long> SeedOrder(string state = "PendingConfirmation", bool paid = true, long? parent = null, long? farm = null)
    {
        string code = Guid.NewGuid().ToString("N");
        await Sql($"""
            DECLARE @order BIGINT={parent?.ToString() ?? "NULL"};
            IF @order IS NULL BEGIN
              INSERT dbo.[Order](BuyerUserId,OrderCode,OrderType,OrderDate,ShippingAddress,TotalAmount,FinalAmount,OrderStatus,RecipientNameSnapshot,RecipientPhoneSnapshot)
              VALUES({BuyerId},'O-{code}','Retail',SYSUTCDATETIME(),N'Test address',150000,150000,'Processing',N'Test buyer','0901234567');
              SET @order=SCOPE_IDENTITY();
            END;
            DECLARE @farm BIGINT=COALESCE({farm?.ToString() ?? "NULL"},(SELECT FarmerProfileId FROM dbo.Product WHERE ProductId={ProductId}));
            INSERT dbo.SellerOrder(OrderId,FarmerProfileId,SellerOrderCode,Subtotal,FinalAmount,Status)
            VALUES(@order,@farm,'S-{code}',150000,150000,'{state}');
            DECLARE @seller BIGINT=SCOPE_IDENTITY();
            INSERT dbo.OrderItem(SellerOrderId,ProductId,Quantity,UnitPriceSnapshot,UnitSnapshot,SubTotal,ItemStatus,ProductNameSnapshot)
            VALUES(@seller,{ProductId},5,30000,'kg',150000,'Pending',N'Test product');
            DECLARE @item BIGINT=SCOPE_IDENTITY();
            INSERT dbo.OrderItemBatchAllocation(OrderItemId,HarvestBatchId,AllocationSequence,AllocatedQuantity,AllocationStatus,ReservedAt)
            VALUES(@item,{BatchId},1,5,'Reserved',SYSUTCDATETIME());
            UPDATE dbo.HarvestBatch SET AvailableQuantity=AvailableQuantity-5,ReservedQuantity=ReservedQuantity+5 WHERE HarvestBatchId={BatchId};
            {(paid && parent is null ? $"INSERT dbo.PaymentTransaction(OrderId,TransactionCode,GatewayProvider,Amount,PaymentType,PaymentStatus,PaidAt,PaymentPurpose,RequestedAt) VALUES(@order,'T-{code}','TestSink',150000,'Transfer','Paid',SYSUTCDATETIME(),'Order',SYSUTCDATETIME());" : "")}
            """);
        return await Number($"SELECT SellerOrderId FROM dbo.SellerOrder WHERE SellerOrderCode='S-{code}'");
    }
    public async Task Fulfill(long id)
    {
        await Ok<ActionResult>(await Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/confirm", new { }));
        await Ok<ActionResult>(await Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/ready", new { }));
        await Ok<ActionResult>(await Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/dispatch", new { }));
    }
    public async ValueTask DisposeAsync() { Farmer?.Dispose(); Buyer?.Dispose(); Other?.Dispose(); Host?.Dispose(); await Database.DisposeAsync(); }
    public ImageInput Image(int size = 16) { var b = new byte[size]; new byte[] {137,80,78,71,13,10,26,10}.CopyTo(b,0); return new("image.png","image/png",b,true); }
    public ProductInput Input() => new("SQL tomato",CategoryId,"kg","Đồng Tháp","Valid SQL product",30000,null,null,[Image()],RequestKey: Guid.NewGuid());
    public async Task<ProductInput> ExistingInput()
    {
        var p = await Farmer.GetFromJsonAsync<Listing>($"/api/farmer/products/{ProductId}");
        return new(p!.Name,p.CategoryId,p.Unit,p.Origin,p.Description,p.RetailPrice,p.WholesaleMinimum,p.WholesalePrice,
            p.Images.Select(i => new ImageInput("","",null,i.Primary,i.Url)).ToArray(),p.QuantityStep,p.Revision,p.Id);
    }
}
