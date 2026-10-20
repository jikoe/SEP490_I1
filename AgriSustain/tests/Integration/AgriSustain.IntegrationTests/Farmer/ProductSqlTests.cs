using System.Net;
using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using Xunit;

namespace AgriSustain.IntegrationTests;

// Real SQL/API integration coverage; never used by the fake-only unit suite.
public sealed class ProductSqlTests
{
    public static IEnumerable<object[]> ProductCases => Enumerable.Range(1,16).Select(n => new object[] {$"IT-FAR-03-{n:000}",n});
    [SqlTheory, MemberData(nameof(ProductCases))]
    public async Task Product_SQL_design(string caseId, int n)
    {
        await using var f = await FarmerRetailSqlFixture.Create();
        Assert.StartsWith("IT-FAR-03",caseId);
        switch(n)
        {
            case 1:
                var created=await f.Ok<ActionResult>(await f.Farmer.PostAsJsonAsync("/api/farmer/products",f.Input()));
                Assert.Equal("MSG71",created.MessageCode);
                Assert.Equal(1,await f.Number($"SELECT COUNT(*) FROM dbo.Product WHERE ProductId={created.Id} AND ApprovalStatus='Pending' AND IsPublished=0"));
                Assert.Equal(1,await f.Number($"SELECT COUNT(*) FROM dbo.ProductImage WHERE ProductId={created.Id}"));
                Assert.Equal(1,await f.Number($"SELECT COUNT(*) FROM dbo.AuditLog WHERE EntityId={created.Id} AND EntityType='Product'")); break;
            case 2:
                await f.Sql("INSERT dbo.[User](Username,Email,PasswordHash,FullName,Role,Status,IsEmailVerified) VALUES('commerce_moderator','moderator@example.test','!TEST!',N'Test Moderator','ContentModerator','Active',1);");
                var pending=await f.Ok<ActionResult>(await f.Farmer.PostAsJsonAsync("/api/farmer/products",f.Input()));
                using(var moderator=f.Host.Client("commerce_moderator")) {
                    await f.Ok<ActionResult>(await moderator.PostAsJsonAsync($"/api/moderation/products/{pending.Id}",new ModerationInput(false,"Rejected origin",1)));
                    var rejected=await f.Farmer.GetFromJsonAsync<Listing>($"/api/farmer/products/{pending.Id}"); Assert.Equal("Rejected origin",rejected!.RejectionReason);
                    Assert.Equal(1,await f.Number("SELECT COUNT(*) FROM dbo.Notification WHERE Title='MSG165' AND Content LIKE '%Rejected origin%'"));
                    await f.Ok<ActionResult>(await f.Farmer.PostAsJsonAsync("/api/farmer/products",f.Input() with {ProductId=pending.Id,Revision=2}));
                    await f.Ok<ActionResult>(await moderator.PostAsJsonAsync($"/api/moderation/products/{pending.Id}",new ModerationInput(true,null,3)));
                    Assert.False((await f.Farmer.GetFromJsonAsync<Listing>($"/api/farmer/products/{pending.Id}"))!.Published);
                    Assert.Equal(1,await f.Number("SELECT COUNT(*) FROM dbo.Notification WHERE Title='MSG29'"));
                } break;
            case 3:
            case 4:
                var list=await f.Farmer.GetFromJsonAsync<PagedResponse<Listing>>($"/api/farmer/products?keyword={(n==4?"no-match-xyz":"")}&categoryId={f.CategoryId}");
                if(n==4) Assert.Empty(list!.Items); else { Assert.NotEmpty(list!.Items); Assert.All(list.Items,p=>Assert.Equal(f.CategoryId,p.CategoryId)); }
                Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.AuditLog")); break;
            case 5:
            case 6:
            case 7:
            case 8:
                var input=await f.ExistingInput();
                if(n==8) await f.Sql($"UPDATE dbo.Product SET ApprovalStatus='Rejected',RejectionReason='Preserved reason' WHERE ProductId={f.ProductId};");
                var response=await (n==8 ? f.Farmer.PostAsJsonAsync("/api/farmer/products",input with {Name="Corrected"}) : f.Farmer.PutAsJsonAsync($"/api/farmer/products/{f.ProductId}",n==5? input with {Name="Changed content"}:n==6?input with {RetailPrice=32000}:input with {Unit="bag"}));
                if(n==7) { await f.Code(response,"MSG79"); Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.AuditLog")); }
                else { await f.Ok<ActionResult>(response); Assert.Equal(n==6?1:0,await f.Number($"SELECT CONVERT(INT,IsPublished) FROM dbo.Product WHERE ProductId={f.ProductId}")); if(n==8) Assert.Equal(1,await f.Number($"SELECT COUNT(*) FROM dbo.Product WHERE ProductId={f.ProductId} AND RejectionReason='Preserved reason'")); } break;
            case 9:
            case 10:
            case 11:
            case 12:
                if(n==11) await f.Sql($"UPDATE dbo.Product SET IsPublished=0 WHERE ProductId={f.ProductId};");
                if(n==12) await f.Sql($"UPDATE dbo.Product SET ApprovalStatus='Pending',IsPublished=0 WHERE ProductId={f.ProductId};");
                var visibility=await f.Farmer.PostAsJsonAsync($"/api/farmer/products/{f.ProductId}/visibility",new {Published=n>=11,Revision=0,Confirmed=n!=10});
                if(n==10) await f.Code(visibility,"MSG73"); else if(n==12) await f.Code(visibility,"MSG131"); else await f.Ok<ActionResult>(visibility);
                Assert.Equal(n is 10 or 11?1:0,await f.Number($"SELECT CONVERT(INT,IsPublished) FROM dbo.Product WHERE ProductId={f.ProductId}")); break;
            case 13:
                await f.Sql($"UPDATE dbo.FarmerProfile SET VerificationStatus='Rejected' WHERE UserId={f.FarmerId}");
                await f.Code(await f.Farmer.PostAsJsonAsync("/api/farmer/products",f.Input()),"MSG75");
                using(var foreign=f.Host.Client("demo3uc_farmer_gold")) Assert.Equal(HttpStatusCode.Forbidden,(await foreign.GetAsync($"/api/farmer/products/{f.ProductId}")).StatusCode);
                Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.AuditLog")); break;
            case 14:
                foreach(var invalid in new[] {f.Input() with {Name=""}, f.Input() with {RetailPrice=0},f.Input() with {Images=[]},f.Input() with {WholesaleMinimum=1,WholesalePrice=1}})
                    Assert.False((await f.Farmer.PostAsJsonAsync("/api/farmer/products",invalid)).IsSuccessStatusCode);
                Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.CommerceMedia")); break;
            case 15:
                await f.Ok<ActionResult>(await f.Farmer.PostAsJsonAsync("/api/farmer/products",f.Input() with {Images=[f.Image(5242880)]}));
                await f.Code(await f.Farmer.PostAsJsonAsync("/api/farmer/products",f.Input() with {Images=[f.Image(5242881)]}),"MSG78");
                await f.Code(await f.Farmer.PostAsJsonAsync("/api/farmer/products",f.Input() with {Images=[f.Image() with {Name="x.gif",ContentType="image/gif"}]}),"MSG78"); break;
            case 16:
                var stale=await f.ExistingInput(); await f.Ok<ActionResult>(await f.Farmer.PutAsJsonAsync($"/api/farmer/products/{f.ProductId}",stale with {RetailPrice=31000}));
                await f.Code(await f.Farmer.PutAsJsonAsync($"/api/farmer/products/{f.ProductId}",stale),"MSG122");
                await f.Sql("CREATE TRIGGER dbo.CommerceTestFault ON dbo.AuditLog AFTER INSERT AS THROW 51251,'Injected failure',1;");
                await f.Code(await f.Farmer.PostAsJsonAsync("/api/farmer/products",f.Input()),"MSG123");
                Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.CommerceMedia"));
                Assert.Equal(0,await f.Number("SELECT COUNT(*) FROM dbo.Product WHERE ProductName='SQL tomato'"));
                await f.Sql("DROP TRIGGER dbo.CommerceTestFault;");
                await f.Ok<ActionResult>(await f.Farmer.PostAsJsonAsync("/api/farmer/products",f.Input())); break;
        }
    }
}
