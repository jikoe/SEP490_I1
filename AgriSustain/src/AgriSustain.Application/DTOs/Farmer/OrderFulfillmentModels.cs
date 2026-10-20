using AgriSustain.Application.DTOs.Shared;

namespace AgriSustain.Application.DTOs.Farmer;

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
