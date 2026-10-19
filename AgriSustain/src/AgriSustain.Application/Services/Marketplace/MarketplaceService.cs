using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Interfaces.Repositories;
using AgriSustain.Application.Interfaces.Services;
using FluentValidation;

namespace AgriSustain.Application.Services.Marketplace;

public sealed class MarketplaceService(
    IMarketplaceRepository repository,
    IValidator<MarketplaceQuery> validator) : IMarketplaceService
{
    public async Task<Result<PagedResponse<ProductSummary>>> BrowseAsync(
        MarketplaceQuery query, CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return Result<PagedResponse<ProductSummary>>.Failure(new Error(
                "MSG40", "Tiêu chí tìm kiếm hoặc bộ lọc không hợp lệ."));
        }

        return Result<PagedResponse<ProductSummary>>.Success(
            await repository.BrowseAsync(query, cancellationToken));
    }

    public async Task<Result<ProductDetail>> FindAsync(
        long id, CancellationToken cancellationToken)
    {
        ProductDetail? product = id > 0 ? await repository.FindAsync(id, cancellationToken) : null;
        return product == null
            ? Result<ProductDetail>.Failure(new Error(
                "MSG16", "Sản phẩm không tồn tại hoặc không còn được công khai.", "NotFound"))
            : Result<ProductDetail>.Success(product);
    }

    public Task<MarketplaceFilters> GetFiltersAsync(CancellationToken cancellationToken) =>
        repository.GetFiltersAsync(cancellationToken);
}
