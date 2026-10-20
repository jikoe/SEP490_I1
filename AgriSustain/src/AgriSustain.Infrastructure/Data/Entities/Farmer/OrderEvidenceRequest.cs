namespace AgriSustain.Infrastructure.Data.Entities;

public sealed class OrderEvidenceRequest
{
    public long Id { get; set; }
    public long DisputeTicketId { get; set; }
    public long RequestedUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string? Response { get; set; }
}
