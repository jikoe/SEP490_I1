using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Data.Demo;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.Repositories.EfCore;

/// <summary>
/// Resolves explicitly permitted local fixture selectors using real SQL account state through EF.
/// MOCK-AUTH: this is not login/JWT authentication. Replace with the authentication module's
/// verified principal and durable session revocation; keep Development/loopback restrictions.
/// </summary>
public sealed class EfDemoIdentityReader(AgriSustainDbContext db,
    TimeProvider clock) : IDemoIdentityReader
{
    /// <summary>Reads only ID/role for a known active, unlocked seed identity; never reads its hash.</summary>
    public async Task<(long Id, string Role)?> FindAsync(string fixture,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        if (long.TryParse(fixture, out long parsedId))
        {
            var byId = await db.User.AsNoTracking()
                .Where(u => u.UserId == parsedId && u.Status == "Active" && (u.LockedUntil == null || u.LockedUntil <= now))
                .Select(u => new { u.UserId, u.Role }).SingleOrDefaultAsync(cancellationToken);
            if (byId != null) return (byId.UserId, byId.Role);
        }

        var directUser = await db.User.AsNoTracking()
            .Where(u => (u.Username == fixture || u.Email == fixture) && u.Status == "Active" && (u.LockedUntil == null || u.LockedUntil <= now))
            .Select(u => new { u.UserId, u.Role }).FirstOrDefaultAsync(cancellationToken);
        if (directUser != null) return (directUser.UserId, directUser.Role);

        string? seedUsername = fixture switch
        {
            "admin" => "demo3uc_admin", "support" => "demo3uc_support",
            "retail-customer" => "demo3uc_retail", "farmer" => "demo3uc_farmer_green",
            "other-retail-customer" => "demo3uc_retail_other", _ => null
        };
        string? mappedUsername = fixture switch
        {
            "admin" => "admin_system",
            "support" => "staff_support",
            "retail-customer" => "buyer_retail",
            "farmer" => "farmer_dalat",
            "other-retail-customer" => "demo3uc_retail_other",
            _ => null
        };

        if (mappedUsername is not null)
        {
            var seedUser = await db.User.AsNoTracking()
                .Where(row => (row.Username == mappedUsername || row.Username == seedUsername) && row.Status == "Active"
                    && (row.LockedUntil == null || row.LockedUntil <= now))
                .Select(row => new { row.UserId, row.Role }).FirstOrDefaultAsync(cancellationToken);
            if (seedUser is not null) return (seedUser.UserId, seedUser.Role);
        }

        return null;
    }
}
