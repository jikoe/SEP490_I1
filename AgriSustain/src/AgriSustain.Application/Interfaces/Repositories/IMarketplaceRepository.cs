using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Marketplace;

namespace AgriSustain.Application.Interfaces.Repositories;

public interface IMarketplaceRepository
{
    Task<PagedResponse<ProductSummary>> BrowseAsync(
        MarketplaceQuery query, CancellationToken cancellationToken);
    Task<ProductDetail?> FindAsync(long id, CancellationToken cancellationToken);
    Task<MarketplaceFilters> GetFiltersAsync(CancellationToken cancellationToken);
}
