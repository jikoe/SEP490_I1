using AgriSustain.Application.DTOs.Farmer;

namespace AgriSustain.Application.DTOs.RetailCustomer;

public sealed class BasketLine
{
    public long Id { get; set; }
    public long ProductId { get; set; }
    public long? BatchId { get; set; }
    public decimal Quantity { get; set; }
    public bool Selected { get; set; }
    public Listing? Offer { get; set; }
    public decimal UnitPrice { get; set; }
    public bool Available { get; set; }
    public string? MessageCode { get; set; }
}
public sealed record CartResponse(BasketLine[] Items, decimal SelectedSubtotal,
    string ReviewToken, bool CanProceedToCheckout = false, string? MessageCode = null);
public sealed record AddCartItem(long ProductId, long BatchId, decimal Quantity);
public sealed record CartSelection(long[] Ids, bool Selected);
public sealed record RemoveCartItems(long[] Ids, bool Confirmed);
public sealed record CheckoutReview(string ReviewToken);
