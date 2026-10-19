using AgriSustain.Application.DTOs.Marketplace;
using FluentValidation;

namespace AgriSustain.Application.Validators.Marketplace;

public sealed class MarketplaceQueryValidator : AbstractValidator<MarketplaceQuery>
{
    public MarketplaceQueryValidator()
    {
        RuleFor(query => query.Keyword).MaximumLength(150);
        RuleFor(query => query.CategoryId).GreaterThan(0).When(query => query.CategoryId != null);
        RuleFor(query => query.SupplierId).GreaterThan(0).When(query => query.SupplierId != null);
        RuleFor(query => query.CropSeasonId).GreaterThan(0)
            .When(query => query.CropSeasonId != null);
        RuleFor(query => query.Region).MaximumLength(100);
        RuleFor(query => query.Certification).MaximumLength(100);
        RuleFor(query => query.MinPrice).GreaterThanOrEqualTo(0);
        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(0);
        RuleFor(query => query.MaxPrice).GreaterThanOrEqualTo(query => query.MinPrice)
            .When(query => query.MinPrice != null && query.MaxPrice != null);
        RuleFor(query => query.MinRating).InclusiveBetween(0, 5);
        RuleFor(query => query.SortBy)
            .Must(sort => sort is "newest" or "price-asc" or "price-desc" or "rating");
        RuleFor(query => query.Page).InclusiveBetween(1, 1000000);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
