using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Application.Services.Shared;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;

namespace AgriSustain.Application.Services.RetailCustomer;

public sealed class CartService(IFarmerRetailUnitOfWork unit, ICurrentUser user, TimeProvider clock)
{
    public Task<Result<CartResponse>> GetAsync(CancellationToken ct = default) => Run(async (r, id) =>
    {
        var lines = await r.CartAsync(id);
        Refresh(lines);
        await r.SaveCartAsync(id, lines);
        return Result<CartResponse>.Success(Response(lines));
    }, ct);
    public Task<Result<CartResponse>> AddItemAsync(AddCartItem input, CancellationToken ct = default) => Run(async (r, id) =>
    {
        var offer = await r.ListingAsync(input.ProductId);
        if (offer is null || !FarmerRetailRules.Saleable(offer)) return Fail("MSG88", "Sản phẩm không thể mua.");
        if (!QuantityValid(input.Quantity, offer.QuantityStep)) return Fail("MSG167", "Số lượng phải > 0 và đúng bước bán.");
        var lines = (await r.CartAsync(id)).ToList();
        var line = lines.SingleOrDefault(x => x.ProductId == input.ProductId && x.BatchId == input.BatchId);
        if (line is null)
        {
            if (lines.Count >= 100) return Fail("MSG120", "Tối đa 100 dòng trong giỏ.");
            line = new BasketLine { ProductId = offer.Id, BatchId = input.BatchId, Offer = offer, Selected = true };
            lines.Add(line);
        }
        line.Quantity += input.Quantity;
        if (!Eligible(line)) return Fail("MSG88", "Lô hàng không còn đủ điều kiện mua.");
        string? message = Normalize(line);
        Refresh(lines.ToArray());
        await r.SaveCartAsync(id, lines.ToArray());
        return Result<CartResponse>.Success(Response(lines.ToArray(), message ?? "MSG81"));
    }, ct);
    public Task<Result<CartResponse>> UpdateQuantityAsync(long lineId, decimal quantity, CancellationToken ct = default) => Run(async (r, id) =>
    {
        var lines = await r.CartAsync(id);
        var line = lines.SingleOrDefault(x => x.Id == lineId);
        if (line is null) return Fail("MSG121", "Không có quyền truy cập dòng giỏ hàng.", "Forbidden");
        if (!QuantityValid(quantity, line.Offer?.QuantityStep ?? 1)) return Fail("MSG167", "Số lượng phải > 0 và đúng bước bán; giá trị cũ được giữ lại.");
        bool wholesaleBefore = line.Offer!.WholesaleMinimum is { } min && line.Quantity >= min;
        line.Quantity = quantity;
        string? message = Normalize(line);
        bool wholesaleNow = line.Offer.WholesaleMinimum is { } next && line.Quantity >= next;
        if (message is null && line.Offer.WholesalePrice.HasValue && wholesaleBefore != wholesaleNow)
            message = wholesaleNow ? "MSG82" : "MSG83";
        Refresh(lines);
        await r.SaveCartAsync(id, lines);
        return Result<CartResponse>.Success(Response(lines, message));
    }, ct);
    public Task<Result<CartResponse>> SetSelectionAsync(CartSelection input, CancellationToken ct = default) => Run(async (r, id) =>
    {
        var lines = await r.CartAsync(id);
        if (!Owned(input.Ids, lines)) return Fail("MSG121", "Không có quyền chọn dòng giỏ hàng.", "Forbidden");
        Refresh(lines);
        foreach (var line in lines.Where(x => input.Ids.Contains(x.Id))) line.Selected = input.Selected && line.Available;
        await r.SaveCartAsync(id, lines);
        return Result<CartResponse>.Success(Response(lines, lines.Any(x => input.Ids.Contains(x.Id) && !x.Available) ? "MSG88" : null));
    }, ct);
    public Task<Result<CartResponse>> RemoveItemsAsync(RemoveCartItems input, CancellationToken ct = default) => Run(async (r, id) =>
    {
        var lines = await r.CartAsync(id);
        if (!Owned(input.Ids, lines)) return Fail("MSG121", "Không có quyền xóa dòng giỏ hàng.", "Forbidden");
        if (!input.Confirmed) return Result<CartResponse>.Success(Response(lines, "MSG84"));
        lines = lines.Where(x => !input.Ids.Contains(x.Id)).ToArray();
        Refresh(lines);
        await r.SaveCartAsync(id, lines);
        return Result<CartResponse>.Success(Response(lines, "MSG85"));
    }, ct);
    public Task<Result<CartResponse>> PrepareCheckoutAsync(string reviewedToken, CancellationToken ct = default) => Run(async (r, id) =>
    {
        var lines = await r.CartAsync(id);
        bool selected = lines.Any(x => x.Selected);
        string? message = null;
        foreach (var line in lines)
        {
            bool wasSelected = line.Selected;
            string? adjustment = Normalize(line);
            if (wasSelected && adjustment is not null) message = adjustment;
        }
        Refresh(lines);
        if (message is null && !selected) message = "MSG87";
        if (message is null && reviewedToken != Token(lines)) message = "MSG118";
        await r.SaveCartAsync(id, lines);
        return Result<CartResponse>.Success(Response(lines, message, message is null));
    }, ct);
    private Task<Result<CartResponse>> Run(Func<IFarmerRetailRepository, long, Task<Result<CartResponse>>> work, CancellationToken ct) => unit.RunAsync(async r =>
    {
        var actor = await FarmerRetailRules.Actor(r, user, "RetailCustomer");
        return actor.IsSuccess ? await work(r, actor.Value!.Id) : Result<CartResponse>.Failure(actor.Error!);
    }, ct);
    private static bool Owned(long[]? ids, BasketLine[] lines) => ids is { Length: > 0 and <= 100 } && ids.Distinct().Count() == ids.Length
        && ids.All(id => lines.Any(x => x.Id == id));
    public static bool QuantityValid(decimal quantity, decimal step) => step > 0 && quantity > 0 && quantity <= 999999999999999m
        && quantity % step == 0 && decimal.Round(quantity, 3) == quantity;
    private bool Eligible(BasketLine line)
    {
        if (line.Offer is not { } offer || !FarmerRetailRules.Saleable(offer)) return false;
        var batch = offer.Batches.SingleOrDefault(b => b.Id == line.BatchId);
        return batch is not null && batch.ProductId == line.ProductId && FarmerRetailRules.BatchEligible(batch,
            DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date))
            && decimal.Floor(batch.Available / offer.QuantityStep) > 0;
    }
    private string? Normalize(BasketLine line)
    {
        if (!Eligible(line)) { line.Available = false; line.Selected = false; line.MessageCode = "MSG88"; return "MSG88"; }
        line.Available = true;
        var p = line.Offer!;
        decimal max = decimal.Floor(p.Batches.Single(b => b.Id == line.BatchId).Available / p.QuantityStep) * p.QuantityStep;
        if (!QuantityValid(line.Quantity, p.QuantityStep) || line.Quantity > max)
        {
            decimal adjusted = decimal.Floor(Math.Min(line.Quantity, max) / p.QuantityStep) * p.QuantityStep;
            if (adjusted <= 0) { line.Available = false; line.Selected = false; line.MessageCode = "MSG88"; return "MSG88"; }
            line.Quantity = adjusted;
            line.MessageCode = "MSG86";
            return "MSG86";
        }
        return null;
    }
    private void Refresh(BasketLine[] lines)
    {
        foreach (var line in lines)
        {
            Normalize(line);
            line.UnitPrice = line.Offer is { } p ? p.WholesalePrice is { } price && line.Quantity >= p.WholesaleMinimum ? price : p.RetailPrice : 0;
        }
    }
    private static string Token(BasketLine[] lines) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(
        lines.OrderBy(x => x.Id).Select(x => new {x.Id, x.ProductId, x.BatchId, Quantity = Canonical(x.Quantity), x.Selected,
            UnitPrice = Canonical(x.UnitPrice), RetailPrice = Canonical(x.Offer?.RetailPrice),
            WholesalePrice = Canonical(x.Offer?.WholesalePrice), WholesaleMinimum = Canonical(x.Offer?.WholesaleMinimum),
            QuantityStep = Canonical(x.Offer?.QuantityStep)})))));
    // SQL DECIMAL preserves scale (2.000); reviewed quantities can arrive as 2. They are equal.
    private static string? Canonical(decimal? number) => number?.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
    private static CartResponse Response(BasketLine[] lines, string? message = null, bool ready = false) => new(lines,
        lines.Where(x => x.Selected && x.Available).Sum(x => x.Quantity * x.UnitPrice), Token(lines), ready,
        message ?? (lines.Length == 0 ? "MSG168" : null));
    private static Result<CartResponse> Fail(string code, string text, string kind = "Validation") => Result<CartResponse>.Failure(FarmerRetailRules.Error(code, text, kind));
}
