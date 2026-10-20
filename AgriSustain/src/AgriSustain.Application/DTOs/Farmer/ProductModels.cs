using AgriSustain.Application.DTOs.Shared;

namespace AgriSustain.Application.DTOs.Farmer;

public sealed record ProductInput(string Name, long CategoryId, string Unit, string Origin,
    string Description, decimal RetailPrice, decimal? WholesaleMinimum, decimal? WholesalePrice,
    ImageInput[] Images, decimal QuantityStep = 1, long Revision = 0, long? ProductId = null, Guid? RequestKey = null);
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
public sealed record ModerationInput(bool Approved, string? Reason, long Revision);
