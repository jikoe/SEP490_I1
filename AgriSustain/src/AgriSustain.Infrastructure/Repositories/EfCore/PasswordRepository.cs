using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

public sealed class PasswordRepository(
    AgriSustainDbContext db, TimeProvider clock) : IPasswordRepository
{
    public Task<string?> FindActiveHashAsync(long userId, CancellationToken cancellationToken)
    {
        DateTime now = clock.GetUtcNow().UtcDateTime;
        return db.User.AsNoTracking()
            .Where(user => user.UserId == userId && user.Status == "Active"
                && (user.LockedUntil == null || user.LockedUntil <= now))
            .Select(user => (string?)user.PasswordHash)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ReplaceAsync(
        long userId, string expectedHash, string newHash, CancellationToken cancellationToken)
    {
        DateTime now = clock.GetUtcNow().UtcDateTime;
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        int changed = await db.User
            .Where(user => user.UserId == userId
                && EF.Functions.Collate(
                    user.PasswordHash, "Latin1_General_100_BIN2") == expectedHash
                && user.Status == "Active"
                && (user.LockedUntil == null || user.LockedUntil <= now))
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                user => user.PasswordHash, newHash), cancellationToken);
        if (changed != 1)
        {
            return false;
        }

        db.AuditLog.Add(new AuditLog
        {
            PerformedByUserId = userId,
            EntityType = "User",
            EntityId = userId,
            Action = "ChangePassword",
            OccurredAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }
}
