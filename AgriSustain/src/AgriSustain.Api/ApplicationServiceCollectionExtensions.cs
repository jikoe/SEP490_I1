using AgriSustain.Application.Interfaces.Identity;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Infrastructure.Data.Contexts;
using AgriSustain.Infrastructure.Repositories;
using AgriSustain.Infrastructure.Security.Authentication;
using AgriSustain.Infrastructure.Services.Authentication;
using AgriSustain.Infrastructure.UnitOfWork;
using Microsoft.EntityFrameworkCore;

namespace AgriSustain.Api;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        return services;
    }
}
