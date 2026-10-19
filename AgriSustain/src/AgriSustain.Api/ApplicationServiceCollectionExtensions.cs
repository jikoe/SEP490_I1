using AgriSustain.Application.DTOs.Administration;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.DTOs.Profiles;
using AgriSustain.Application.Interfaces.Services;
using AgriSustain.Application.Services.Administration;
using AgriSustain.Application.Services.Authentication;
using AgriSustain.Application.Services.Marketplace;
using AgriSustain.Application.Services.Orders;
using AgriSustain.Application.Services.Profiles;
using AgriSustain.Application.Validators.Administration;
using AgriSustain.Application.Validators.Authentication;
using AgriSustain.Application.Validators.Marketplace;
using AgriSustain.Application.Validators.Orders;
using AgriSustain.Application.Validators.Profiles;
using FluentValidation;

namespace AgriSustain.Api;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAccountStatusService, AccountStatusService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICheckoutService, CheckoutService>();
        services.AddScoped<IAuthenticationService, AuthenticationService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IOrderTrackingService, OrderTrackingService>();
        services.AddScoped<AgriSustain.Application.Services.Commerce.ProductService>();
        services.AddScoped<AgriSustain.Application.Services.Commerce.CartService>();
        services.AddScoped<AgriSustain.Application.Services.Commerce.OrderFulfillmentService>();
        services.AddScoped<IPasswordChangeService, PasswordChangeService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IMarketplaceService, MarketplaceService>();
        services.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordValidator>();
        services.AddScoped<IValidator<MarketplaceQuery>, MarketplaceQueryValidator>();
        services.AddScoped<IValidator<AddCartItemRequest>, AddCartItemValidator>();

        services.AddScoped<IValidator<UserQuery>, UserQueryValidator>();
        services.AddScoped<IValidator<AccountActionRequest>, AccountActionValidator>();
        services.AddScoped<IValidator<CategoryQuery>, CategoryQueryValidator>();
        services.AddScoped<IValidator<SaveCategoryRequest>, SaveCategoryValidator>();
        services.AddScoped<IValidator<CheckoutInput>, CheckoutInputValidator>();
        services.AddScoped<IValidator<PlaceOrderRequest>, PlaceOrderValidator>();

        services.AddScoped<IValidator<RegisterRequest>, RegisterRequestValidator>();
        services.AddScoped<IValidator<VerifyOtpRequest>, VerifyOtpRequestValidator>();
        services.AddScoped<IValidator<ResendOtpRequest>, ResendOtpRequestValidator>();
        services.AddScoped<IValidator<UpdateConsumerProfileRequest>, UpdateConsumerProfileValidator>();
        services.AddScoped<IValidator<UpdateFarmerProfileRequest>, UpdateFarmerProfileValidator>();
        services.AddScoped<IValidator<OrderQuery>, OrderQueryValidator>();

        return services;
    }
}
