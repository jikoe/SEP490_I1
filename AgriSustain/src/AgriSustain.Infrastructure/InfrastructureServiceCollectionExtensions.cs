using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgriSustain.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Register Data, Repositories, UnitOfWork, Payments, Email, FileStorage, Auditing and BackgroundWorkers here.
        return services;
    }
}
