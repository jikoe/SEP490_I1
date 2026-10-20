namespace AgriSustain.Infrastructure.Data.Entities;

public sealed class StoredMedia
{
    public Guid Id { get; set; }
    public long OwnerUserId { get; set; }
    public string ContentType { get; set; } = "";
    public byte[] Content { get; set; } = [];
    public bool PublicProductImage { get; set; }
}
