using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using Xunit;

namespace AgriSustain.IntegrationTests;

// Real SQL/API integration coverage; never used by the fake-only unit suite.
public sealed class OrderFulfillmentSqlTests
{
    public static IEnumerable<object[]> OrderCases => Enumerable.Range(1,19).Select(n => new object[] {$"IT-FAR-06-{n:000}",n});
    [SqlTheory, MemberData(nameof(OrderCases))]
    public async Task Fulfillment_SQL_design(string caseId,int n)
    {
        await using var f=await FarmerRetailSqlFixture.Create(); Assert.StartsWith("IT-FAR-06",caseId);
        long id=await f.SeedOrder(n==18?"ReadyToShip":"PendingConfirmation",n!=6&&n!=9);
        switch(n)
        {
            case 1:
            case 8:
            case 13:
                var list=await f.Farmer.GetFromJsonAsync<PagedResponse<FulfillmentOrder>>("/api/seller-orders?keyword="+(n==13?"no-match-xyz":""));
                if(n==13) Assert.Empty(list!.Items); else Assert.Single(list!.Items);
                Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.OrderStatusHistory")); break;
            case 2:
                await f.Ok<ActionResult>(await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/confirm",new{}));
                Assert.Equal(5,await f.Number($"SELECT ReservedQuantity FROM dbo.HarvestBatch WHERE HarvestBatchId={f.BatchId}"));
                Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.InventoryMovement")); break;
            case 3:
            case 4:
            case 5:
            case 10:
                if(n==5) {long parent=await f.Number($"SELECT OrderId FROM dbo.SellerOrder WHERE SellerOrderId={id}"); long foreignFarm=await f.Number("SELECT FarmerProfileId FROM dbo.FarmerProfile WHERE UserId=(SELECT UserId FROM dbo.[User] WHERE Username='demo3uc_farmer_gold')"); await f.SeedOrder(parent:parent,farm:foreignFarm);}
                await f.Fulfill(id); Assert.Equal(1,await f.Number("SELECT COUNT(*) FROM dbo.Shipment"));
                Assert.Equal(1,await f.Number("SELECT COUNT(*) FROM dbo.InventoryMovement WHERE MovementType='Shipment'"));
                if(n==4) {var first=await f.Ok<ActionResult>(await f.Buyer.PostAsJsonAsync($"/api/seller-orders/{id}/receive",new{}));
                    var delivered=await f.Buyer.GetFromJsonAsync<FulfillmentOrder>($"/api/seller-orders/{id}"); Assert.NotNull(delivered!.DeliveredAt);
                    await f.Ok<ActionResult>(await f.Buyer.PostAsJsonAsync($"/api/seller-orders/{id}/accept",new{}));
                    Assert.Equal(delivered.DeliveredAt,(await f.Buyer.GetFromJsonAsync<FulfillmentOrder>($"/api/seller-orders/{id}"))!.DeliveredAt); }
                if(n==5) Assert.Equal(1,await f.Number("SELECT COUNT(*) FROM dbo.[Order] WHERE OrderStatus='PartiallyShipped'"));
                if(n==10) {await f.Ok<ActionResult>(await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/delivery-issue",new EvidenceInput("Returned damaged produce",[f.Image()])));
                    Assert.Equal(15,await f.Number($"SELECT AvailableQuantity FROM dbo.HarvestBatch WHERE HarvestBatchId={f.BatchId}")); Assert.Equal(1,await f.Number("SELECT COUNT(*) FROM dbo.DisputeEvidence"));} break;
            case 6:
            case 7:
            case 16:
                if(n==16) {await f.Code(await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/reject",new OrderAction("Quality","12345678901234",true)),"MSG70");}
                await f.Ok<ActionResult>(await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/reject",new OrderAction("Quality","123456789012345",true)));
                Assert.Equal(0,await f.Number($"SELECT ReservedQuantity FROM dbo.HarvestBatch WHERE HarvestBatchId={f.BatchId}"));
                Assert.Equal(n==6?0:1,await f.Number("SELECT COUNT(*) FROM dbo.CommerceRefundRequest WHERE Status='PendingFinance'"));
                Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.RefundTransaction")); break;
            case 9:
                await f.Code(await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/confirm",new{}),"MSG148"); Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.OrderStatusHistory")); break;
            case 11:
            case 12:
                await f.Sql($"INSERT dbo.DisputeTicket(SubmittedByUserId,SellerOrderId,DisputeReason,TicketStatus,CreatedAt) VALUES({f.BuyerId},{id},'Quality','Open',SYSUTCDATETIME()); DECLARE @ticket BIGINT=SCOPE_IDENTITY(); INSERT dbo.CommerceEvidenceRequest(DisputeTicketId,RequestedUserId,RequestedAt) VALUES(@ticket,{f.FarmerId},DATEADD(minute,{(n==11?-2879:-2881)},SYSUTCDATETIME()));");
                long requestId=await f.Number("SELECT MAX(Id) FROM dbo.CommerceEvidenceRequest");
                var response=await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/evidence",new EvidenceInput("Packaging evidence for review",[f.Image()],requestId));
                if(n==12) {await f.Code(response,"MSG166"); Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.DisputeEvidence"));}
                else {await f.Ok<ActionResult>(response); Assert.Equal(1,await f.Number("SELECT COUNT(*) FROM dbo.CommerceEvidenceRequest WHERE RespondedAt IS NOT NULL"));} break;
            case 14:
                await f.Sql($"UPDATE dbo.HarvestBatch SET Status='Recalled' WHERE HarvestBatchId={f.BatchId}");
                await f.Code(await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/confirm",new{}),"MSG139"); Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.Shipment")); break;
            case 15:
                await f.Code(await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/dispatch",new{}),"MSG131");
                await f.Ok<ActionResult>(await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/confirm",new{}));
                await f.Ok<ActionResult>(await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/ready",new{}));
                var results=await Task.WhenAll(f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/dispatch",new{}),f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/dispatch",new{}));
                Assert.Single(results,r=>r.IsSuccessStatusCode); Assert.Equal(1,await f.Number("SELECT COUNT(*) FROM dbo.Shipment"));
                Assert.Equal(1,await f.Number("SELECT COUNT(*) FROM dbo.Notification WHERE Title='MSG65'")); break;
            case 17:
                using(var other=f.Host.Client("demo3uc_farmer_gold")) {Assert.Equal(HttpStatusCode.Forbidden,(await other.GetAsync($"/api/seller-orders/{id}")).StatusCode); Assert.Equal(HttpStatusCode.Forbidden,(await other.PostAsJsonAsync($"/api/seller-orders/{id}/confirm",new{})).StatusCode);}
                Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.OrderStatusHistory")); break;
            case 18:
                await f.Sql("CREATE TRIGGER dbo.CommerceTestFault ON dbo.AuditLog AFTER INSERT AS THROW 51251,'Injected failure',1;");
                await f.Code(await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/dispatch",new{}),"MSG123");
                Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.Shipment")); Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.InventoryMovement")); Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.Notification"));
                Assert.Equal(5,await f.Number($"SELECT ReservedQuantity FROM dbo.HarvestBatch WHERE HarvestBatchId={f.BatchId}"));
                await f.Sql("DROP TRIGGER dbo.CommerceTestFault;"); await f.Ok<ActionResult>(await f.Farmer.PostAsJsonAsync($"/api/seller-orders/{id}/dispatch",new{})); break;
            case 19:
                await f.Sql("EXEC sp_rename 'dbo.SellerOrder', 'CommerceTestHiddenOrders';");
                try {Assert.Equal(HttpStatusCode.ServiceUnavailable,(await f.Farmer.GetAsync("/api/seller-orders")).StatusCode);} finally {await f.Sql("EXEC sp_rename 'dbo.CommerceTestHiddenOrders', 'SellerOrder';");}
                break;
        }
    }
}
