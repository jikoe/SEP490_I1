using System.Data;
using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Repositories.EfCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Infrastructure.UnitOfWork;

public sealed class CommerceUnitOfWork(AgriSustainDbContext db, TimeProvider clock) : ICommerceUnitOfWork
{
    public async Task<Result<T>> RunAsync<T>(Func<ICommerceRepository, Task<Result<T>>> work, CancellationToken cancellationToken = default, bool readOnly = false)
    {
        db.ChangeTracker.Clear();
        try
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            // Share the checkout lock so cross-host cart/stock operations cannot race placement.
            await db.Database.ExecuteSqlRawAsync("""
                DECLARE @Result int;
                EXEC @Result=sys.sp_getapplock @Resource='AgriSustain.ThreeUc.SqlDemo',
                    @LockMode='Exclusive',@LockOwner='Transaction',@LockTimeout=10000;
                IF @Result<0 THROW 51140,'Commerce transaction conflict',1;
                """, cancellationToken);
            var result = await work(new CommerceRepository(db, clock, cancellationToken));
            if (result.IsSuccess && !readOnly) { await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
            else await transaction.RollbackAsync(cancellationToken);
            return result;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is DbUpdateConcurrencyException || ex is SqlException {Number: 1205 or 2601 or 2627 or 51140})
        { return Result<T>.Failure(new Error("MSG122", "Dữ liệu đã thay đổi hoặc yêu cầu đã xử lý. Hãy tải lại.", "Conflict")); }
        catch (Exception ex) when (ex is DbUpdateException or SqlException or InvalidOperationException or System.IO.IOException)
        { return Result<T>.Failure(new Error(readOnly ? "MSG126" : "MSG123", readOnly ? "Không thể tải dữ liệu. Vui lòng thử lại." : "Không thể lưu dữ liệu. Không có thay đổi nào được cam kết.", "Unavailable")); }
        finally { db.ChangeTracker.Clear(); }
    }
}
