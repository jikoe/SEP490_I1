namespace AgriSustain.Infrastructure.Data.Entities;

public partial class Product
{
    public long Revision { get; set; }
    public decimal QuantityStep { get; set; } = 1;
}
public partial class CartItem { public long? HarvestBatchId { get; set; } }
public partial class SellerOrder { public decimal? RequiredPaymentAmount { get; set; } }
public sealed class CommerceSubmission
{
    public long ActorId { get; set; }
    public Guid RequestKey { get; set; }
    public long ProductId { get; set; }
}
public sealed class CommerceMedia
{
    public Guid Id { get; set; }
    public long OwnerUserId { get; set; }
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public bool PublicProductImage { get; set; }
}
public sealed class CommerceEvidenceRequest
{
    public long Id { get; set; }
    public long DisputeTicketId { get; set; }
    public long RequestedUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? Response { get; set; }
}
public sealed class CommerceRefundRequest
{
    public long Id { get; set; }
    public long SellerOrderId { get; set; }
    public string Reason { get; set; } = "";
    public string Status { get; set; } = "PendingFinance";
    public DateTime CreatedAt { get; set; }
}
