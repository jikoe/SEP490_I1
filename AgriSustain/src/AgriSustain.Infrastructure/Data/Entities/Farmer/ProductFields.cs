namespace AgriSustain.Infrastructure.Data.Entities;

public partial class Product
{
    public long Revision { get; set; }
    public decimal QuantityStep { get; set; } = 1;
}
public sealed class ProductSubmission
{
    public long ActorId { get; set; }
    public Guid RequestKey { get; set; }
    public long ProductId { get; set; }
}
