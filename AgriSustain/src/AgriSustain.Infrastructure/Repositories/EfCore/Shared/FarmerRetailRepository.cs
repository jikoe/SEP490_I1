using System.Text.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Services.Shared;
using AgriSustain.Application.Services.Farmer;
using AgriSustain.Application.Services.RetailCustomer;
using AgriSustain.Infrastructure.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using D = AgriSustain.Infrastructure.Data.Entities;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

internal sealed partial class FarmerRetailRepository : IFarmerRetailRepository
{
    private readonly AgriSustainDbContext db;
    private readonly TimeProvider clock;
    private readonly CancellationToken ct;
    public FarmerRetailRepository(AgriSustainDbContext db, TimeProvider clock, CancellationToken ct)
    { this.db=db; this.clock=clock; this.ct=ct; }
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7)).Date);
    private void Notify(long userId, string code, string text) => db.Notification.Add(new D.Notification {
        UserId = userId, Title = code, Content = text, NotificationType = code, DeliveryChannel = "InApp", DeliveryStatus = "Queued", SentAt = Now, RelatedRecordType = "SellerOrder" });
    private void Audit(long actor, string entity, long id, string action, string? oldValue, string newValue, string? reason = null) =>
        db.AuditLog.Add(new D.AuditLog { PerformedByUserId = actor, EntityType = entity, EntityId = id,
            Action = action, OldValue = oldValue, NewValue = newValue, Reason = reason, OccurredAt = Now });
}
