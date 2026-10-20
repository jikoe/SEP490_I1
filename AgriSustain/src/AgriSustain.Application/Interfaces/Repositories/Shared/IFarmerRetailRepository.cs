using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Shared;
using AgriSustain.Application.DTOs.Farmer;
using AgriSustain.Application.DTOs.RetailCustomer;

namespace AgriSustain.Application.Interfaces.Repositories;

// A transaction-scoped repository. Implementations must persist mutations, audit and outbox
// notifications together; no external email or payment calls occur inside this boundary.
public interface IFarmerRetailRepository
{
    Task<FarmerRetailActor?> ActorAsync(long id);
    Task<CategoryOption[]> CategoriesAsync();
    Task<bool> CategoryActiveAsync(long id);
    Task<PagedResponse<Listing>> ListingsAsync(long? farmId, FarmerRetailQuery query);
    Task<Listing?> ListingAsync(long id);
    Task<long> SaveListingAsync(Listing listing, ImageInput[]? images, long actorId, string action);
    Task<long?> SubmissionAsync(long actorId, Guid key);
    Task RecordSubmissionAsync(long actorId, Guid key, long productId);
    Task<BasketLine[]> CartAsync(long userId);
    Task SaveCartAsync(long userId, BasketLine[] lines);
    Task<PagedResponse<FulfillmentOrder>> OrdersAsync(long actorId, bool buyer, FarmerRetailQuery query);
    Task<FulfillmentOrder?> OrderAsync(long id, long actorId, bool buyer);
    Task SaveOrderAsync(FulfillmentOrder order, long actorId, string action, string? reason = null);
    Task SaveEvidenceAsync(FulfillmentOrder order, long actorId, EvidenceInput input, bool failure);
}
