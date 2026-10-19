using System.Data;
using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.UnitOfWork;
using AgriSustain.Infrastructure.Data.Demo;
using AgriSustain.Infrastructure.Repositories.Sql;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace AgriSustain.Infrastructure.UnitOfWork;

/// <summary>
/// Persists the three UCs in SQL Server while keeping explicitly missing dependencies mocked.
/// A database application lock serializes local-demo writes across API processes. This broad
/// lock favors reviewability for the local demo, not production throughput; the SQL team can
/// replace it with reviewed row/range lock ordering without changing application contracts.
/// </summary>
public sealed class SqlThreeUcUnitOfWork(
    IConfiguration configuration, ICurrentUser actor,
    SqlDemoDependencies mocks, TimeProvider clock) : IThreeUcUnitOfWork
{
    /// <summary>
    /// Runs a consistent SQL read and discards accidental mutations. Read-model queries page and
    /// filter in SQL; they do not hydrate entire business tables into the old demo store.
    /// </summary>
    public Task<Result<T>> ReadAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, CancellationToken cancellationToken) =>
        RunAsync(operation, false, cancellationToken);

    /// <summary>
    /// Commits SQL business/audit/cart/reservation changes and then their mock projections only
    /// when the entire application callback succeeds. Failures roll back the SQL transaction.
    /// </summary>
    public Task<Result<T>> ExecuteAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, CancellationToken cancellationToken) =>
        RunAsync(operation, true, cancellationToken);

    /// <summary>
    /// Owns connection, transaction, cross-process demo write lock and local mock-state gate.
    /// Constraint/concurrency conflicts are sanitized; unexpected SQL failures reach API logging.
    /// No schema for DB-06 request keys or DB-09 point balances is silently introduced.
    /// </summary>
    private async Task<Result<T>> RunAsync<T>(
        Func<IThreeUcSession, Task<Result<T>>> operation, bool commit,
        CancellationToken cancellationToken)
    {
        await mocks.Gate.WaitAsync(cancellationToken);
        try
        {
            await using var connection = new SqlConnection(
                configuration.GetConnectionString("AgriSustain"));
            await connection.OpenAsync(cancellationToken);
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            if (commit)
            {
                await using var command = new SqlCommand("""
                    DECLARE @Result int;
                    EXEC @Result=sys.sp_getapplock @Resource='AgriSustain.ThreeUc.SqlDemo',
                        @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
                    IF @Result<0 THROW 51140,'Demo transaction lock unavailable.',1;
                    """, connection, transaction);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            var session = new SqlThreeUcSession(connection, transaction, actor.UserId, clock, mocks);
            try
            {
                var result = await operation(session);
                cancellationToken.ThrowIfCancellationRequested();
                if (commit && result.IsSuccess)
                {
                    await transaction.CommitAsync(cancellationToken);
                    mocks.Publish(session.Points, session.RequestOrders, session.SessionVersions);
                }
                else
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                return result;
            }
            catch (SqlException exception) when (exception.Number is 547 or 2601 or 2627 or 1205)
            {
                if (transaction.Connection != null)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                return Result<T>.Failure(new Error("SQL_CONFLICT",
                    "Dữ liệu liên quan đã thay đổi hoặc vẫn còn tham chiếu. Vui lòng tải lại.",
                    "Conflict"));
            }
        }
        finally
        {
            mocks.Gate.Release();
        }
    }
}
