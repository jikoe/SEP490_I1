namespace AgriSustain.Application.DTOs.Orders;

// Actual-batch association is optional for legacy carts; new UC-RC-01 lines always carry it.
public sealed record CartLine(long Id, long ProductId, decimal Quantity, bool Selected, long? BatchId = null);
