using AgriSustain.Application.Common;
using AgriSustain.Application.Interfaces.Repositories;

namespace AgriSustain.Application.Interfaces.UnitOfWork;

public interface IFarmerRetailUnitOfWork
{
    Task<Result<T>> RunAsync<T>(Func<IFarmerRetailRepository, Task<Result<T>>> work,
        CancellationToken cancellationToken = default, bool readOnly = false);
}
