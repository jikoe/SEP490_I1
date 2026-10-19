using AgriSustain.Application.DTOs.Marketplace;
using FluentValidation;

namespace AgriSustain.Application.Validators.Marketplace;

public sealed class AddCartItemValidator : AbstractValidator<AddCartItemRequest>
{
    public AddCartItemValidator()
    {
        RuleFor(request => request.ProductId).GreaterThan(0);
        RuleFor(request => request.Quantity).InclusiveBetween(0.001m, 1000000m)
            .Must(quantity => decimal.Round(quantity, 3) == quantity);
    }
}
