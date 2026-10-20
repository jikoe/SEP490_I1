using AgriSustain.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Data.Contexts;

public partial class AgriSustainDbContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>().Property(p => p.Revision).IsConcurrencyToken();
        modelBuilder.Entity<Product>().Property(p => p.QuantityStep).HasPrecision(18, 3);
        modelBuilder.Entity<CartItem>().HasOne<HarvestBatch>().WithMany().HasForeignKey(x => x.HarvestBatchId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<SellerOrder>().Property(x => x.RequiredPaymentAmount).HasPrecision(18, 2);
        modelBuilder.Entity<ProductSubmission>(e => {
            e.ToTable("CommerceSubmission"); e.HasKey(x => new {x.ActorId, x.RequestKey});
            e.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<StoredMedia>(e => {
            e.ToTable("CommerceMedia"); e.HasKey(x => x.Id); e.Property(x => x.ContentType).HasMaxLength(50);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<OrderEvidenceRequest>(e => {
            e.ToTable("CommerceEvidenceRequest"); e.HasKey(x => x.Id); e.Property(x => x.Response).HasMaxLength(1000);
            e.HasOne<DisputeTicket>().WithMany().HasForeignKey(x => x.DisputeTicketId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<User>().WithMany().HasForeignKey(x => x.RequestedUserId).OnDelete(DeleteBehavior.NoAction);
        });
        modelBuilder.Entity<OrderRefundRequest>(e => {
            e.ToTable("CommerceRefundRequest"); e.HasKey(x => x.Id); e.HasIndex(x => x.SellerOrderId).IsUnique();
            e.Property(x => x.Reason).HasMaxLength(1300); e.Property(x => x.Status).HasMaxLength(50);
            e.HasOne<SellerOrder>().WithMany().HasForeignKey(x => x.SellerOrderId).OnDelete(DeleteBehavior.NoAction);
        });
    }
}
