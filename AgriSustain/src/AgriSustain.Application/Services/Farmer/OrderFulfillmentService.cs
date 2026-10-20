using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Application.Services.Shared;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;

namespace AgriSustain.Application.Services.Farmer;

public sealed class OrderFulfillmentService(IFarmerRetailUnitOfWork unit, ICurrentUser user, TimeProvider clock)
{
    public Task<Result<PagedResponse<FulfillmentOrder>>> GetListAsync(FarmerRetailQuery q, bool buyer = false, CancellationToken ct = default) => unit.RunAsync(async r =>
    {
        var actor = await FarmerRetailRules.Actor(r, user, buyer ? "RetailCustomer" : "Farmer", !buyer);
        if (!actor.IsSuccess) return Result<PagedResponse<FulfillmentOrder>>.Failure(actor.Error!);
        if (FarmerRetailRules.Query(q) is { } error) return Result<PagedResponse<FulfillmentOrder>>.Failure(error);
        return Result<PagedResponse<FulfillmentOrder>>.Success(await r.OrdersAsync(actor.Value!.Id, buyer, q));
    }, ct, readOnly: true);
    public Task<Result<FulfillmentOrder>> GetAsync(long id, bool buyer = false, CancellationToken ct = default) => unit.RunAsync(async r =>
    {
        var actor = await FarmerRetailRules.Actor(r, user, buyer ? "RetailCustomer" : "Farmer", !buyer);
        if (!actor.IsSuccess) return Result<FulfillmentOrder>.Failure(actor.Error!);
        var order = await r.OrderAsync(id, actor.Value!.Id, buyer);
        return order is null ? Result<FulfillmentOrder>.Failure(Denied()) : Result<FulfillmentOrder>.Success(order);
    }, ct, readOnly: true);
    public Task<Result<ActionResult>> ConfirmAsync(long id, CancellationToken ct = default) => Transition(id, "Confirm", null, false, ct);
    public Task<Result<ActionResult>> MarkReadyAsync(long id, CancellationToken ct = default) => Transition(id, "Ready", null, false, ct);
    public Task<Result<ActionResult>> DispatchAsync(long id, CancellationToken ct = default) => Transition(id, "Dispatch", null, false, ct);
    public Task<Result<ActionResult>> RejectAsync(long id, OrderAction input, CancellationToken ct = default) => Transition(id, "Reject", input, false, ct);
    public Task<Result<ActionResult>> ReceiveAsync(long id, bool accept = false, CancellationToken ct = default) => Transition(id, accept ? "Accept" : "Receive", null, true, ct);
    private Task<Result<ActionResult>> Transition(long id, string action, OrderAction? input, bool buyer, CancellationToken ct) => unit.RunAsync(async r =>
    {
        var actor = await FarmerRetailRules.Actor(r, user, buyer ? "RetailCustomer" : "Farmer", !buyer);
        if (!actor.IsSuccess) return Result<ActionResult>.Failure(actor.Error!);
        var order = await r.OrderAsync(id, actor.Value!.Id, buyer);
        if (order is null) return Result<ActionResult>.Failure(Denied());
        if (order.ContractOrder) return Fail("MSG131", "Giao hàng hợp đồng được xử lý theo UC-FAR-08.");
        string[] from = action switch {
            "Confirm" => ["Pending", "PendingConfirmation", "PendingPayment"],
            "Ready" => ["Confirmed", "Preparing"], "Dispatch" => ["ReadyToShip"],
            "Reject" => ["Pending", "PendingConfirmation", "PendingPayment", "Confirmed", "Preparing", "ReadyToShip"],
            "Receive" => ["Shipping", "InTransit"], "Accept" => ["Delivered"], _ => [] };
        string target = action switch {"Confirm" => "Confirmed", "Ready" => "ReadyToShip", "Dispatch" => "Shipping",
            "Reject" => "Cancelled", "Receive" => "Delivered", _ => "Received"};
        if (!from.Contains(order.Status)) return Fail(order.Status == target || order.History.Any(h => h.Status == target) ? "MSG122" : "MSG131", "Trạng thái không hợp lệ hoặc thao tác đã được thực hiện.");
        if (action == "Reject" && (input?.Confirmed != true || string.IsNullOrWhiteSpace(input.Reason)
            || input.Reason.Length > 255 || (input.Explanation?.Trim().Length ?? 0) < 15 || input.Explanation!.Length > 1000))
            return Fail("MSG70", "Chọn lý do, nhập giải thích ít nhất 15 ký tự và xác nhận.");
        if (action is "Confirm" or "Ready" or "Dispatch")
        {
            if (order.VerifiedPayment < order.RequiredPayment) return Fail("MSG148", "Chưa xác minh đủ thanh toán hoặc tiền cọc.");
            if (order.Lines.Length == 0 || order.Lines.Any(l => l.Allocations.Length == 0)) return Fail("MSG148", "Chưa phân bổ lô thu hoạch thực tế.");
            var date = DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date);
            if (order.ExpectedDelivery is { } expected && expected > date) date = expected;
            if (order.Lines.SelectMany(l => l.Allocations).Any(a => !FarmerRetailRules.BatchEligible(a.Batch, date)))
                return Fail("MSG139", "Có lô hết hạn, bị thu hồi hoặc chưa nhập kho thực tế.");
            if (order.Lines.Any(l => l.Quantity <= 0 || l.Allocations.Sum(a => a.Quantity) != l.Quantity
                || l.Allocations.Any(a => a.Quantity <= 0 || a.Batch.ProductId != l.ProductId || a.Status is not ("Reserved" or "Committed")))
                || order.Lines.SelectMany(l => l.Allocations).GroupBy(a => a.Batch.Id).Any(g => g.Sum(a => a.Quantity) > g.First().Batch.Reserved))
                return Fail("MSG120", "Lượng phân bổ không khớp số lượng đặt hoặc số lượng đã giữ.");
        }
        if (action == "Receive") order.DeliveredAt = clock.GetUtcNow().UtcDateTime;
        if (action == "Accept") order.ReceivedAt = clock.GetUtcNow().UtcDateTime;
        order.Status = target;
        await r.SaveOrderAsync(order, actor.Value.Id, action, input is null ? null : input.Reason + ": " + input.Explanation);
        string code = action switch {"Confirm" => "MSG64", "Ready" => "MSG146", "Dispatch" => "MSG66", "Reject" => "MSG67", _ => "MSG147"};
        return Result<ActionResult>.Success(new(code, id));
    }, ct);
    public Task<Result<ActionResult>> EvidenceAsync(long id, EvidenceInput input, bool failure, CancellationToken ct = default) => unit.RunAsync(async r =>
    {
        var actor = await FarmerRetailRules.Actor(r, user, "Farmer", true);
        if (!actor.IsSuccess) return Result<ActionResult>.Failure(actor.Error!);
        var order = await r.OrderAsync(id, actor.Value!.Id, false);
        if (order is null) return Result<ActionResult>.Failure(Denied());
        if (string.IsNullOrWhiteSpace(input.Description) || input.Description.Length > 1000 || input.Files is null || input.Files.Length > 10)
            return Fail("MSG120", "Nhập giải trình tối đa 1000 ký tự và tối đa 10 tệp.");
        foreach (var file in input.Files) if (FarmerRetailRules.Image(file, false) is { } invalid) return Result<ActionResult>.Failure(invalid);
        if (failure && (order.Status is not ("Shipping" or "InTransit" or "Delivered" or "Received") || input.Files.Length == 0))
            return Fail("MSG131", "Báo cáo giao thất bại/hàng trả cần bằng chứng và trạng thái giao hàng phù hợp.");
        if (!failure)
        {
            var request = order.EvidenceRequests.SingleOrDefault(x => x.Id == input.RequestId);
            if (request is null) return Result<ActionResult>.Failure(Denied());
            if (request.RespondedAt is not null) return Fail("MSG122", "Yêu cầu bằng chứng đã được phản hồi.");
            if (clock.GetUtcNow().UtcDateTime > request.RequestedAt.AddHours(48)) return Fail("MSG166", "Đã hết hạn phản hồi bằng chứng 48 giờ.");
        }
        await r.SaveEvidenceAsync(order, actor.Value.Id, input, failure);
        return Result<ActionResult>.Success(new(failure ? "MSG149" : "MSG150", id));
    }, ct);
    private static Error Denied() => FarmerRetailRules.Error("MSG121", "Không có quyền truy cập đơn hàng.", "Forbidden");
    private static Result<ActionResult> Fail(string code, string message) => Result<ActionResult>.Failure(FarmerRetailRules.Error(code, message, code is "MSG122" or "MSG131" ? "Conflict" : "Validation"));
}
