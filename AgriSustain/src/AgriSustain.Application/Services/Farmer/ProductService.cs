using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Application.Services.Shared;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;

namespace AgriSustain.Application.Services.Farmer;

public sealed class ProductService(IFarmerRetailUnitOfWork unit, ICurrentUser user)
{
    public Task<Result<PagedResponse<Listing>>> GetListAsync(FarmerRetailQuery query, CancellationToken ct = default) => unit.RunAsync(async r =>
    {
        var actor = await FarmerRetailRules.Actor(r, user, "Farmer");
        if (!actor.IsSuccess) return Result<PagedResponse<Listing>>.Failure(actor.Error!);
        if (FarmerRetailRules.Query(query) is { } error) return Result<PagedResponse<Listing>>.Failure(error);
        if (actor.Value!.FarmId is null) return Result<PagedResponse<Listing>>.Success(new([], 0, query.Page, query.PageSize));
        return Result<PagedResponse<Listing>>.Success(await r.ListingsAsync(actor.Value.FarmId, query));
    }, ct, readOnly: true);
    public Task<Result<Listing>> GetAsync(long id, CancellationToken ct = default) => unit.RunAsync(async r =>
    {
        var actor = await FarmerRetailRules.Actor(r, user, "Farmer");
        if (!actor.IsSuccess) return Result<Listing>.Failure(actor.Error!);
        var p = await r.ListingAsync(id);
        return p is null || p.FarmId != actor.Value!.FarmId ? Result<Listing>.Failure(Denied()) : Result<Listing>.Success(p);
    }, ct, readOnly: true);
    public Task<Result<ActionResult>> SubmitAsync(ProductInput request, CancellationToken ct = default) => Save(request.ProductId, request, true, ct);
    public Task<Result<ActionResult>> UpdateAsync(long id, ProductInput request, CancellationToken ct = default) => Save(id, request, false, ct);
    private Task<Result<ActionResult>> Save(long? id, ProductInput input, bool submit, CancellationToken ct) => unit.RunAsync(async r =>
    {
        var actor = await FarmerRetailRules.Actor(r, user, "Farmer", true);
        if (!actor.IsSuccess) return Result<ActionResult>.Failure(actor.Error!);
        if (id is null)
        {
            if (input.RequestKey is null || input.RequestKey == Guid.Empty)
                return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG120", "Thiếu mã yêu cầu gửi duyệt."));
            if (await r.SubmissionAsync(actor.Value!.Id, input.RequestKey.Value) is { } existing)
                return Result<ActionResult>.Success(new("MSG71", existing));
        }
        var p = id.HasValue ? await r.ListingAsync(id.Value) : new Listing { FarmId = actor.Value!.FarmId!.Value };
        if (p is null || p.FarmId != actor.Value!.FarmId) return Result<ActionResult>.Failure(Denied());
        if (id.HasValue && p.Revision != input.Revision) return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG122", "Dữ liệu đã thay đổi. Hãy tải lại.", "Conflict"));
        if (id.HasValue && p.UnitLocked && p.Unit != input.Unit.Trim())
            return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG79", "Đơn vị đã có lô hoặc đơn hàng tham chiếu và không thể sửa."));
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Length > 255 || string.IsNullOrWhiteSpace(input.Unit)
            || input.Unit.Length > 50 || string.IsNullOrWhiteSpace(input.Origin) || input.Origin.Length > 100
            || string.IsNullOrWhiteSpace(input.Description) || input.Description.Length > 10000
            || input.QuantityStep <= 0 || input.QuantityStep > 999999999999999m || decimal.Round(input.QuantityStep, 3) != input.QuantityStep
            || !await r.CategoryActiveAsync(input.CategoryId))
            return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG120", "Điền đúng tên, danh mục đang hoạt động, đơn vị, xuất xứ, mô tả và bước số lượng."));
        if (FarmerRetailRules.Prices(input.RetailPrice, input.WholesaleMinimum, input.WholesalePrice) is { } prices)
            return Result<ActionResult>.Failure(prices);
        if (input.Images is null || input.Images.Count(i => i.Primary) != 1)
            return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG76", "Chọn đúng một ảnh đại diện."));
        if (input.Images.Length > 10) return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG78", "Tối đa 10 ảnh."));
        foreach (var image in input.Images)
        {
            if (FarmerRetailRules.Image(image, id.HasValue) is { } invalid) return Result<ActionResult>.Failure(invalid);
            if (image.ExistingUrl is not null && image.Content is null && !p.Images.Any(i => i.Url == image.ExistingUrl))
                return Result<ActionResult>.Failure(Denied());
        }
        bool contentChanged = p.Name != input.Name.Trim() || p.CategoryId != input.CategoryId || p.Unit != input.Unit.Trim()
            || p.QuantityStep != input.QuantityStep || p.Origin != input.Origin.Trim() || p.Description != input.Description.Trim()
            || input.Images.Any(i => i.Content is not null) || !p.Images.SequenceEqual(input.Images.Select(i => new ImageView(i.ExistingUrl ?? "", i.Primary)));
        p.Name = input.Name.Trim(); p.CategoryId = input.CategoryId; p.Unit = input.Unit.Trim(); p.QuantityStep = input.QuantityStep;
        p.Origin = input.Origin.Trim(); p.Description = input.Description.Trim(); p.RetailPrice = input.RetailPrice;
        p.WholesaleMinimum = input.WholesaleMinimum; p.WholesalePrice = input.WholesalePrice;
        if (submit || contentChanged) { p.Approval = "Pending"; p.Published = false; }
        p.Revision++;
        long saved = await r.SaveListingAsync(p, input.Images, actor.Value!.Id, submit ? "Submit" : "Update");
        if (id is null) await r.RecordSubmissionAsync(actor.Value.Id, input.RequestKey!.Value, saved);
        return Result<ActionResult>.Success(new(submit ? "MSG71" : "MSG72", saved));
    }, ct);
    public Task<Result<ActionResult>> SetVisibilityAsync(long id, bool published, long revision, bool confirmed, CancellationToken ct = default) => unit.RunAsync(async r =>
    {
        var actor = await FarmerRetailRules.Actor(r, user, "Farmer", true);
        if (!actor.IsSuccess) return Result<ActionResult>.Failure(actor.Error!);
        var p = await r.ListingAsync(id);
        if (p is null || p.FarmId != actor.Value!.FarmId) return Result<ActionResult>.Failure(Denied());
        if (p.Revision != revision) return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG122", "Dữ liệu đã thay đổi.", "Conflict"));
        if (!published && !confirmed) return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG73", "Hãy xác nhận ẩn sản phẩm."));
        if (published && p.Approval != "Approved") return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG131", "Chỉ sản phẩm đã duyệt mới được hiển thị.", "Conflict"));
        if (published && (!p.CategoryActive || !p.FarmEligible || p.Images.Count(i => i.Primary) != 1 || FarmerRetailRules.Prices(p.RetailPrice, p.WholesaleMinimum, p.WholesalePrice) is not null))
            return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG120", "Sản phẩm không còn đủ điều kiện hiển thị."));
        p.Published = published; p.Revision++;
        await r.SaveListingAsync(p, null, actor.Value.Id, published ? "Publish" : "Hide");
        return Result<ActionResult>.Success(new(published ? "MSG164" : "MSG74", id));
    }, ct);
    private static Error Denied() => FarmerRetailRules.Error("MSG80", "Không có quyền truy cập sản phẩm này.", "Forbidden");
    public Task<Result<ActionResult>> ModerateAsync(long id, ModerationInput input, CancellationToken ct = default) => unit.RunAsync(async r =>
    {
        var actor = await FarmerRetailRules.Actor(r, user, "ContentModerator");
        if (!actor.IsSuccess) return Result<ActionResult>.Failure(actor.Error!);
        var p = await r.ListingAsync(id);
        if (p is null) return Result<ActionResult>.Failure(Denied());
        if (p.Revision != input.Revision || p.Approval != "Pending")
            return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG122", "Phiên bản hoặc trạng thái kiểm duyệt đã thay đổi.", "Conflict"));
        if (!input.Approved && (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 500))
            return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG120", "Nhập lý do từ chối tối đa 500 ký tự."));
        if (input.Approved && (!p.FarmEligible || !p.CategoryActive || p.Images.Count(i => i.Primary) != 1
            || FarmerRetailRules.Prices(p.RetailPrice, p.WholesaleMinimum, p.WholesalePrice) is not null))
            return Result<ActionResult>.Failure(FarmerRetailRules.Error("MSG120", "Sản phẩm không đủ điều kiện duyệt."));
        p.Approval = input.Approved ? "Approved" : "Rejected"; p.Published = false;
        p.RejectionReason = input.Approved ? null : input.Reason!.Trim(); p.Revision++;
        await r.SaveListingAsync(p, null, actor.Value!.Id, input.Approved ? "Approve" : "Reject");
        return Result<ActionResult>.Success(new(input.Approved ? "MSG29" : "MSG165", id));
    }, ct);
}
