using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Marketplace;

namespace AgriSustain.Application.Interfaces.Services;

public interface IMarketplaceService
{
    Task<Result<PagedResponse<ProductSummary>>> BrowseAsync(
        MarketplaceQuery query, CancellationToken cancellationToken);
    Task<Result<ProductDetail>> FindAsync(long id, CancellationToken cancellationToken);
    Task<MarketplaceFilters> GetFiltersAsync(CancellationToken cancellationToken);
}
