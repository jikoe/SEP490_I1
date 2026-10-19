using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Interfaces.UnitOfWork;
using FluentValidation;

namespace AgriSustain.Application.Services.Orders;

public sealed class OrderTrackingService(
    IThreeUcUnitOfWork unitOfWork,
    ICurrentUser currentUser,
    IValidator<OrderQuery> queryValidator) : IOrderTrackingService
{
    public async Task<Result<PagedResponse<OrderSummaryDto>>> GetOrderHistoryAsync(
        OrderQuery query, CancellationToken cancellationToken)
    {
        Error? error = await RequestValidation.CheckAsync(queryValidator, query, cancellationToken);
        if (error != null)
        {
            return Result<PagedResponse<OrderSummaryDto>>.Failure(error);
        }

        return await unitOfWork.ReadAsync(async session =>
        {
            if (currentUser.UserId == null)
            {
                return Result<PagedResponse<OrderSummaryDto>>.Failure(new Error(
                    "UNAUTHORIZED", "Vui lòng đăng nhập để xem lịch sử mua hàng.", "Unauthorized"));
            }

            var orders = await session.Orders.GetOrdersByBuyerAsync(
                currentUser.UserId.Value, query, cancellationToken);

            return Result<PagedResponse<OrderSummaryDto>>.Success(orders);
        }, cancellationToken);
    }

    public Task<Result<OrderDetailDto>> GetOrderDetailAsync(
        long orderId, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async session =>
        {
            if (currentUser.UserId == null)
            {
                return Result<OrderDetailDto>.Failure(new Error(
                    "UNAUTHORIZED", "Vui lòng đăng nhập để xem chi tiết đơn hàng.", "Unauthorized"));
            }

            long buyerId = currentUser.UserId.Value;
            var order = await session.Orders.GetOrderDetailAsync(orderId, buyerId, cancellationToken);
            if (order == null)
            {
                return Result<OrderDetailDto>.Failure(new Error(
                    "ORDER_NOT_FOUND", "Không tìm thấy đơn hàng hoặc bạn không có quyền xem đơn hàng này.", "NotFound"));
            }

            await session.Orders.MarkOrderNotificationsReadAsync(orderId, buyerId, cancellationToken);
            return Result<OrderDetailDto>.Success(order);
        }, cancellationToken);

    public Task<Result<OrderDetailDto>> GetOrderDetailByCodeAsync(
        string orderCode, CancellationToken cancellationToken) =>
        unitOfWork.ExecuteAsync(async session =>
        {
            if (currentUser.UserId == null)
            {
                return Result<OrderDetailDto>.Failure(new Error(
                    "UNAUTHORIZED", "Vui lòng đăng nhập để xem chi tiết đơn hàng.", "Unauthorized"));
            }

            long buyerId = currentUser.UserId.Value;
            var order = await session.Orders.GetOrderDetailByCodeAsync(orderCode, buyerId, cancellationToken);
            if (order == null)
            {
                return Result<OrderDetailDto>.Failure(new Error(
                    "ORDER_NOT_FOUND", "Không tìm thấy đơn hàng với mã được yêu cầu.", "NotFound"));
            }

            await session.Orders.MarkOrderNotificationsReadAsync(order.OrderId, buyerId, cancellationToken);
            return Result<OrderDetailDto>.Success(order);
        }, cancellationToken);
}
