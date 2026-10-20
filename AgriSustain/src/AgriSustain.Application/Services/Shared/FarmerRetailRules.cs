using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;

namespace AgriSustain.Application.Services.Shared;

public static class FarmerRetailRules
{
    public static Error Error(string code, string message, string kind = "Validation") => new(code, message, kind);
    public static async Task<Result<FarmerRetailActor>> Actor(IFarmerRetailRepository repository, ICurrentUser user,
        string role, bool approved = false)
    {
        var actor = user.UserId is { } id ? await repository.ActorAsync(id) : null;
        if (actor is null) return Result<FarmerRetailActor>.Failure(Error("MSG121", "Vui lòng đăng nhập.", "Unauthorized"));
        if (actor.Role != role || actor.Status != "Active" || actor.Locked)
            return Result<FarmerRetailActor>.Failure(Error("MSG121", "Bạn không có quyền thực hiện thao tác.", "Forbidden"));
        if (approved && (!actor.FarmApproved || actor.FarmId is null))
            return Result<FarmerRetailActor>.Failure(Error("MSG75", "Hồ sơ nông trại chưa được phê duyệt.", "Forbidden"));
        return Result<FarmerRetailActor>.Success(actor);
    }
    public static Error? Query(FarmerRetailQuery q) => q.Page < 1 || q.Page > 1000000 || q.PageSize is < 1 or > 100
        || q.Keyword?.Length > 200 || q.From > q.To ? Error("MSG120", "Bộ lọc hoặc số trang không hợp lệ.") : null;
    public static Error? Prices(decimal retail, decimal? minimum, decimal? wholesale) =>
        retail <= 0 || retail > 9999999999999999m || (minimum is null) != (wholesale is null)
        || minimum is <= 1 || wholesale is <= 0 || wholesale > retail
            ? Error("MSG77", "Giá lẻ phải > 0; giá sỉ > 0 và ≤ giá lẻ; ngưỡng sỉ > 1. Cặp giá sỉ có thể để trống.") : null;
    public static Error? Image(ImageInput image, bool allowExisting)
    {
        if (allowExisting && image.Content is null && image.ExistingUrl is { Length: > 0 and <= 500 }) return null;
        var data = image.Content;
        if (data is null || data.Length < 8 || data.Length > 5 * 1024 * 1024 || image.Name.Length > 255)
            return Error("MSG78", "Ảnh phải là JPG/JPEG/PNG, tối đa 5 MB mỗi ảnh.");
        bool png = image.ContentType == "image/png" && Path.GetExtension(image.Name).Equals(".png", StringComparison.OrdinalIgnoreCase)
            && data.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10});
        bool jpeg = image.ContentType == "image/jpeg" && new[] {".jpg", ".jpeg"}.Contains(Path.GetExtension(image.Name).ToLowerInvariant())
            && data[0] == 255 && data[1] == 216 && data[2] == 255;
        return png || jpeg ? null : Error("MSG78", "Nội dung hoặc định dạng ảnh không hợp lệ.");
    }
    public static bool BatchEligible(BatchOffer b, DateOnly date) => b.Harvest is not null && b.StockEntered
        && b.Status is "Available" or "InStock" or "FullyReserved"
        && (b.Expiry is null || b.Expiry > date);
    public static bool Saleable(Listing p) => p.Approval == "Approved" && p.Published && p.CategoryActive
        && p.FarmEligible && p.Images.Count(i => i.Primary) == 1 && Prices(p.RetailPrice, p.WholesaleMinimum, p.WholesalePrice) is null;
}
