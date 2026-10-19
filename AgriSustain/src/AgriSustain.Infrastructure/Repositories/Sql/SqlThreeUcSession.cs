using System.Diagnostics;
using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.UnitOfWork;
using Microsoft.Data.SqlClient;

namespace AgriSustain.Infrastructure.Repositories.Sql;

/// <summary>
/// Binds the three repository contracts and audit to one SQL connection/transaction. Separate
/// partial files contain account/category and checkout mappings; no other UC is implemented.
/// </summary>
internal sealed partial class SqlThreeUcSession :
    IThreeUcSession, IAccountRepository, ICategoryRepository, ICheckoutRepository
{
    private readonly SqlConnection _connection;
    private readonly SqlTransaction _transaction;
    private readonly long? _actorId;
    private readonly TimeProvider _clock;
    internal Dictionary<long, int> Points { get; }
    internal Dictionary<(long Buyer, Guid Key), AgriSustain.Domain.Entities.Ordering.RetailOrder>
        RequestOrders { get; }
    internal Dictionary<long, int> SessionVersions { get; }
    public IAccountRepository Accounts => this;
    public ICategoryRepository Categories => this;
    public ICheckoutRepository Checkout => this;

    /// <summary>
    /// Receives a transaction owned by the UoW and detached mock dependency projections. Only
    /// the UoW publishes those projections after a successful SQL commit.
    /// </summary>
    internal SqlThreeUcSession(
        SqlConnection connection, SqlTransaction transaction, long? actorId,
        TimeProvider clock, Data.Demo.SqlDemoDependencies mocks)
    {
        _connection = connection;
        _transaction = transaction;
        _actorId = actorId;
        _clock = clock;
        Points = new(mocks.Points);
        RequestOrders = new(mocks.RequestOrders);
        SessionVersions = new(mocks.SessionVersions);
    }

    /// <summary>
    /// Creates a parameterized command on the existing transaction; no SQL value is interpolated
    /// into command text. Table/column identifiers are fixed in reviewed adapter code.
    /// </summary>
    private SqlCommand Command(string sql, params (string Name, object? Value)[] parameters)
    {
        var command = new SqlCommand(sql, _connection, _transaction);
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }
        return command;
    }

    /// <summary>
    /// Executes a parameterized mutation without committing. The caller may check affected rows
    /// for optimistic stock/state eligibility; the enclosing UoW owns rollback on any exception.
    /// </summary>
    private async Task<int> ExecuteAsync(
        string sql, CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = Command(sql, parameters);
        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Reads a scalar count or SQL-generated identity inside this transaction. Null is zero only
    /// for scalar lookups explicitly defined that way by their caller.
    /// </summary>
    private async Task<long> ScalarAsync(
        string sql, CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = Command(sql, parameters);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken) ?? 0);
    }

    /// <summary>
    /// Materializes a bounded query result and closes its reader before another command runs.
    /// All mapping functions are explicit; no entity/property name is assumed to be a SQL column.
    /// </summary>
    private async Task<List<T>> QueryAsync<T>(
        string sql, Func<SqlDataReader, T> map, CancellationToken cancellationToken,
        params (string Name, object? Value)[] parameters)
    {
        await using var command = Command(sql, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var result = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(map(reader));
        }
        return result;
    }

    /// <summary>
    /// Restores a nullable string without converting missing data into an empty or invented value.
    /// </summary>
    private static string? Text(SqlDataReader reader, string column) =>
        reader[column] is DBNull ? null : (string)reader[column];

    /// <summary>
    /// Treats approved datetime2 values as UTC. Legacy timestamps are not rewritten; data owners
    /// must reconcile legacy timezone conventions before importing historical rows.
    /// </summary>
    private static DateTimeOffset Utc(SqlDataReader reader, string column) =>
        new(DateTime.SpecifyKind((DateTime)reader[column], DateTimeKind.Utc));

    /// <summary>
    /// Persists actor, target, before/after, reason and server trace alongside the business change.
    /// The target has no FK, so deleting an unused category does not destroy its audit identity.
    /// </summary>
    public async Task AddAuditAsync(AuditRecord record, CancellationToken cancellationToken)
    {
        await ExecuteAsync("""
            INSERT dbo.AuditLog(PerformedByUserId,EntityType,EntityId,Action,
                OldValue,NewValue,Reason,OccurredAt,TraceId)
            VALUES(@Actor,@Entity,@Target,@Action,@Old,@New,@Reason,@At,@Trace)
            """, cancellationToken, ("@Actor", record.ActorId), ("@Entity", record.EntityType),
            ("@Target", record.TargetId), ("@Action", record.Action),
            ("@Old", record.OldValue), ("@New", record.NewValue), ("@Reason", record.Reason),
            ("@At", record.OccurredAt.UtcDateTime),
            ("@Trace", Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N")));
    }
}
