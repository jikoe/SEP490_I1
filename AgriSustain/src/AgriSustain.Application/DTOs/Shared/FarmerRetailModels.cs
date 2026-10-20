namespace AgriSustain.Application.DTOs.Shared;

public sealed record FarmerRetailActor(long Id, string Role, string Status, long? FarmId,
    bool FarmApproved, bool Locked = false);
public sealed record ImageInput(string Name, string ContentType, byte[]? Content, bool Primary,
    string? ExistingUrl = null);
public sealed record FarmerRetailQuery(string? Keyword = null, string? Status = null,
    bool? Published = null, long? CategoryId = null, int Page = 1, int PageSize = 10,
    DateTime? From = null, DateTime? To = null, string? OrderType = null, bool? Paid = null);
public sealed record ActionResult(string MessageCode, long Id = 0);
