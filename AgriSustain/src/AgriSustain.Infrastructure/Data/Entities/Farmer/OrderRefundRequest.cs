namespace AgriSustain.Infrastructure.Data.Entities;

public sealed class OrderRefundRequest
{
    public long Id { get; set; }
    public long SellerOrderId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "PendingFinance";
    public DateTime CreatedAt { get; set; }
}
