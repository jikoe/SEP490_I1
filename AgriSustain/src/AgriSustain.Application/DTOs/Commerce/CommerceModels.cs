namespace AgriSustain.Application.DTOs.Commerce;

public sealed record CommerceActor(long Id, string Role, string Status, long? FarmId,
    bool FarmApproved, bool Locked = false);
public sealed record ImageInput(string Name, string ContentType, byte[]? Content, bool Primary,
    string? ExistingUrl = null);
public sealed record ProductInput(string Name, long CategoryId, string Unit, string Origin,
    string Description, decimal RetailPrice, decimal? WholesaleMinimum, decimal? WholesalePrice,
    ImageInput[] Images, decimal QuantityStep = 1, long Revision = 0, long? ProductId = null, Guid? RequestKey = null);
public sealed record CommerceQuery(string? Keyword = null, string? Status = null,
    bool? Published = null, long? CategoryId = null, int Page = 1, int PageSize = 10,
    DateTime? From = null, DateTime? To = null, string? OrderType = null, bool? Paid = null);
public sealed record CategoryOption(long Id, string Name);
public sealed record ImageView(string Url, bool Primary);
public sealed class Listing
{
    public long Id { get; set; }
    public long FarmId { get; set; }
    public string FarmName { get; set; } = "";
    public string Name { get; set; } = "";
    public long CategoryId { get; set; }
    public bool CategoryActive { get; set; }
    public string Unit { get; set; } = "";
    public decimal QuantityStep { get; set; } = 1;
    public string Origin { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal RetailPrice { get; set; }
    public decimal? WholesaleMinimum { get; set; }
    public decimal? WholesalePrice { get; set; }
    public string Approval { get; set; } = "Pending";
    public bool Published { get; set; }
    public bool FarmEligible { get; set; }
    public bool UnitLocked { get; set; }
    public string? RejectionReason { get; set; }
    public long Revision { get; set; }
    public ImageView[] Images { get; set; } = [];
    public BatchOffer[] Batches { get; set; } = [];
    public decimal AvailableQuantity { get; set; }
}
public sealed record BatchOffer(long Id, long ProductId, string Code, decimal Available,
    decimal Reserved, string Status, DateOnly? Harvest, DateOnly? Expiry, bool StockEntered);
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
public sealed record ActionResult(string MessageCode, long Id = 0);
public sealed record ModerationInput(bool Approved, string? Reason, long Revision);
public sealed record OrderAction(string? Reason = null, string? Explanation = null, bool Confirmed = false);
public sealed record EvidenceInput(string Description, ImageInput[] Files, long? RequestId = null);
public sealed record EvidenceRequestView(long Id, long TicketId, DateTime RequestedAt,
    DateTime? RespondedAt, string? Response);
public sealed record FulfillmentLine(long Id, long ProductId, string Name, string Unit,
    decimal Quantity, decimal UnitPrice, AllocationView[] Allocations);
public sealed record AllocationView(long Id, decimal Quantity, string Status, BatchOffer Batch);
public sealed record HistoryView(string Status, string? Reason, DateTime At);
public sealed record ComplaintView(long Id, string Status, string[] RefundStatuses);
public sealed class FulfillmentOrder
{
    public string FarmName { get; set; } = "";
    public bool FarmVerified { get; set; }
    public ComplaintView[] Complaints { get; set; } = [];
    public string[] PayoutStatuses { get; set; } = [];
    public long Id { get; set; }
    public long ParentId { get; set; }
    public long FarmId { get; set; }
    public long BuyerId { get; set; }
    public string Code { get; set; } = "";
    public string ParentCode { get; set; } = "";
    public string Type { get; set; } = "";
    public bool PreOrder { get; set; }
    public bool ContractOrder { get; set; }
    public string Status { get; set; } = "";
    public decimal VerifiedPayment { get; set; }
    public decimal RequiredPayment { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateOnly? ExpectedDelivery { get; set; }
    public string Recipient { get; set; } = "";
    public string BuyerName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string? Notes { get; set; }
    public FulfillmentLine[] Lines { get; set; } = [];
    public HistoryView[] History { get; set; } = [];
    public EvidenceRequestView[] EvidenceRequests { get; set; } = [];
    public bool RefundPending { get; set; }
}
