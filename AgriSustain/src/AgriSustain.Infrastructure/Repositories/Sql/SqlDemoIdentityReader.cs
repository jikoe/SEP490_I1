using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Infrastructure.Data.Demo;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AgriSustain.Infrastructure.Repositories.Sql;

/// <summary>
/// Maps public local fixture selectors to seed usernames and reads current SQL account state.
/// This is NOT password/JWT authentication. The API loopback/Development guard remains required.
/// The real auth team must replace this class and the process-local revocation projection.
/// </summary>
public sealed class SqlDemoIdentityReader(
    IConfiguration configuration, SqlDemoDependencies mocks, TimeProvider clock)
    : IDemoIdentityReader
{
    /// <summary>
    /// Resolves stable semantic usernames rather than IDENTITY values; rejects suspended, locked
    /// or locally revoked identities. Does not expose PasswordHash or accept arbitrary usernames.
    /// </summary>
    public async Task<(long Id, string Role)?> FindAsync(
        string fixture, CancellationToken cancellationToken)
    {
        string? username = fixture switch
        {
            "admin" => "demo3uc_admin",
            "support" => "demo3uc_support",
            "retail-customer" => "demo3uc_retail",
            "other-retail-customer" => "demo3uc_retail_other",
            _ => null
        };
        if (username == null)
        {
            return null;
        }
        await using var connection = new SqlConnection(
            configuration.GetConnectionString("AgriSustain"));
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UserId,Role FROM dbo.[User]
            WHERE Username=@Username AND Status='Active'
                AND (LockedUntil IS NULL OR LockedUntil<=@Now)
            """;
        command.Parameters.AddWithValue("@Username", username);
        command.Parameters.AddWithValue("@Now", clock.GetUtcNow().UtcDateTime);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }
        long id = reader.GetInt64(0);
        return mocks.SessionVersions.GetValueOrDefault(id) == 0
            ? (id, reader.GetString(1)) : null;
    }
}
