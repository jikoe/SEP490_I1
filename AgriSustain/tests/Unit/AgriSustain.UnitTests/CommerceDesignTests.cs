using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Commerce;
using AgriSustain.Application.Services.Commerce;
using Xunit;

namespace AgriSustain.UnitTests;

// Every report row has its own discoverable case ID. Parameter variants reset collaborators.
public sealed class CommerceDesignTests
{
    public static IEnumerable<object[]> ProductCases => Enumerable.Range(1, 21).Select(n => new object[] {$"UT-FAR-03-{n:000}", n});
    public static IEnumerable<object[]> CartCases => Enumerable.Range(1, 22).Select(n => new object[] {$"UT-RC-01-{n:000}", n});
    public static IEnumerable<object[]> OrderCases => Enumerable.Range(1, 16).Select(n => new object[] {$"UT-FAR-06-{n:000}", n});
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
            var r = new CommerceDouble(); var service = new ProductService(r, new CommerceUser(1)); var input = Input();
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
    [Theory, MemberData(nameof(CartCases))]
    public async Task Cart_design(string caseId, int n)
    {
        Assert.StartsWith("UT-RC-01", caseId);
        foreach (var invalidQuantity in n == 10 ? new[] {0m, -1, 1.5m} : new[] {1m})
        {
            var r = new CommerceDouble(); r.Retail(); var clock = new CommerceTestClock(); var service = new CartService(r, new CommerceUser(2), clock);
            Result<CartResponse> result;
            if (n <= 5)
            {
                if (n is 2 or 3 or 4) r.Lines = [r.Line(qty: n == 4 ? 4 : 2)];
                if (n == 4) { r.Product.Batches = [CommerceDouble.Batch() with {Available = 5}]; r.Lines[0].Offer = CommerceDouble.Copy(r.Product); }
                if (n == 5) r.Product.Batches = [CommerceDouble.Batch() with {Available = 0}];
                result = await service.AddItemAsync(new(1, n == 3 ? 21 : 20, n == 4 ? 2 : n == 1 || n == 5 ? 2 : 3));
                if (n == 5) { Code(result, "MSG88"); Assert.Empty(r.Lines); }
                else { Assert.True(result.IsSuccess); Assert.Equal(n == 3 ? 2 : 1, result.Value!.Items.Length); Assert.Equal(n == 4 ? "MSG86" : "MSG81", result.Value.MessageCode); Assert.Equal(n == 1 ? 2 : n == 3 ? 2 : 5, result.Value.Items[0].Quantity); Assert.Equal(n == 1 ? 60000 : n == 3 ? 150000 : 150000, result.Value.SelectedSubtotal); }
            }
            else if (n <= 12)
            {
                r.Lines = [r.Line(qty: n == 6 ? 10 : 3)];
                if (n == 9) { r.Lines[0].Offer!.WholesaleMinimum = null; r.Lines[0].Offer!.WholesalePrice = null; }
                if (n is 11 or 12) r.Lines[0].Offer!.Batches = [CommerceDouble.Batch() with {Available = n == 11 ? 2.4m : .5m}];
                decimal qty = n switch {6 => 9, 7 => 10, 8 => 11, 9 => 10, 10 => invalidQuantity, 11 => 3, _ => 1};
                result = await service.UpdateQuantityAsync(1, qty);
                if (n == 10) { Code(result, "MSG167"); Assert.Equal(3, r.Lines[0].Quantity); Assert.Equal(0, r.CartSaves); }
                else { Assert.True(result.IsSuccess); var l = result.Value!.Items[0]; Assert.Equal(n == 11 ? 2 : n == 12 ? 1 : qty, l.Quantity); Assert.Equal(n is 7 or 8 ? 25000 : 30000, l.UnitPrice); if (n == 12) { Assert.False(l.Selected); Assert.False(l.Available); } if (n == 6) Assert.Equal("MSG83", result.Value.MessageCode); if (n == 7) Assert.Equal("MSG82", result.Value.MessageCode); }
            }
            else if (n <= 16)
            {
                r.Lines = [r.Line(), r.Line(2, 1, false)];
                if (n == 15) r.Lines[0].Offer!.Batches = [CommerceDouble.Batch() with {Available = 0}];
                result = await service.SetSelectionAsync(new(n == 16 ? [99] : n == 15 ? [1, 2] : [1], n != 14));
                if (n == 16) { Code(result, "MSG121"); Assert.Equal(0, r.CartSaves); }
                else { Assert.True(result.IsSuccess); Assert.Equal(n == 13 ? 60000 : n == 14 ? 0 : 30000, result.Value!.SelectedSubtotal); if (n is 14 or 15) Assert.False(result.Value.Items[0].Selected); }
            }
            else
            {
                r.Lines = [r.Line(1, n == 19 ? 5 : 2), r.Line(2, 1, false)];
                if (n == 18) foreach (var l in r.Lines) l.Selected = false;
                string token = (await service.GetAsync()).Value!.ReviewToken;
                if (n == 19) r.Lines[0].Offer!.Batches = [CommerceDouble.Batch() with {Available = 3}];
                if (n == 20) { r.Lines[0].Offer!.Batches = [CommerceDouble.Batch() with {Status = "Recalled"}]; r.Lines[1].Selected = true; }
                if (n == 21) r.Lines[0].Offer!.RetailPrice = 32000;
                if (n == 22) { r.Lines[0].Selected = false; r.Lines[0].Offer!.Batches = [CommerceDouble.Batch() with {Available = 0}]; r.Lines[1].Selected = true; token = (await service.GetAsync()).Value!.ReviewToken; }
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
    [Theory, MemberData(nameof(OrderCases))]
    public async Task Fulfillment_design(string caseId, int n)
    {
        Assert.StartsWith("UT-FAR-06", caseId);
        int variants = n == 5 || n == 8 ? 3 : 1;
        for (int v = 0; v < variants; v++)
        {
            var r = new CommerceDouble(); var service = new OrderFulfillmentService(r, new CommerceUser(1), new CommerceTestClock());
            if (n == 2) r.Order.VerifiedPayment = 0;
            if (n == 3) { r.Order.PreOrder = true; r.Order.RequiredPayment = 20; r.Order.VerifiedPayment = 20; }
            if (n == 4) { r.Order.PreOrder = true; r.Order.Lines = [r.Order.Lines[0] with {Allocations = []}]; }
            if (n == 5) r.Order.Lines = [r.Order.Lines[0] with {Allocations = [r.Order.Lines[0].Allocations[0] with {Batch = CommerceDouble.Batch() with {Status = v == 0 ? "Recalled" : v == 1 ? "Expired" : "Blocked"}}]}];
            if (n == 6) r.Order.Status = "Confirmed";
            if (n is >= 7 and <= 10) r.Order.Status = n == 8 ? new[] {"Pending", "Cancelled", "Shipping"}[v] : n == 10 ? "ReadyToShip" : "Confirmed";
            if (n == 9) r.Order.FarmId = 99;
            if (n >= 11) r.Order.Status = "ReadyToShip";
            if (n == 12) r.Order.Lines = [r.Order.Lines[0] with {Allocations = []}];
            if (n == 13) r.Order.Lines = [r.Order.Lines[0] with {Allocations = [r.Order.Lines[0].Allocations[0] with {Batch = CommerceDouble.Batch() with {Status = "Recalled"}}]}];
            if (n == 14) r.Order.Status = "Pending";
            if (n == 15) r.Order.Status = "Shipping";
            if (n == 16) r.ThrowOnCommit = true;
            var result = n <= 6 ? await service.ConfirmAsync(100) : n <= 10 ? await service.MarkReadyAsync(100) : await service.DispatchAsync(100);
            string? expected = n switch {2 or 4 or 12 => "MSG148", 5 or 13 => "MSG139", 6 or 10 or 15 => "MSG122", 8 or 14 => "MSG131", 9 => "MSG121", 16 => "MSG123", _ => null};
            if (expected is not null) { Code(result, expected); Assert.Equal(0, r.Commits); if (n != 16) Assert.Equal(0, r.OrderSaves); }
            else { Assert.True(result.IsSuccess); Assert.Equal(n <= 6 ? "MSG64" : n <= 10 ? "MSG146" : "MSG66", result.Value!.MessageCode); Assert.Equal(1, r.OrderSaves); Assert.Equal(1, r.Commits); Assert.Equal(n <= 6 ? "Confirm" : n <= 10 ? "Ready" : "Dispatch", r.LastAction); }
        }
    }
}
