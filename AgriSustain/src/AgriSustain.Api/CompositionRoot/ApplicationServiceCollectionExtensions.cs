namespace AgriSustain.Api.CompositionRoot;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register Application.UseCases services and validators here.
        return services;
    }
}
