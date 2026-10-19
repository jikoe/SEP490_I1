using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Orders;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface IOrderTrackingRepository
{
    Task<PagedResponse<OrderSummaryDto>> GetOrdersByBuyerAsync(
        long buyerId, OrderQuery query, CancellationToken cancellationToken);

    Task<OrderDetailDto?> GetOrderDetailAsync(
        long orderId, long? buyerId, CancellationToken cancellationToken);

    Task<OrderDetailDto?> GetOrderDetailByCodeAsync(
        string orderCode, long? buyerId, CancellationToken cancellationToken);

    Task MarkOrderNotificationsReadAsync(
        long orderId, long buyerId, CancellationToken cancellationToken);
}
