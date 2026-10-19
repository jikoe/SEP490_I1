using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Application.Validators.Marketplace;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class MarketplaceValidationTests
{
    [Theory]
    [InlineData(-1, 100, 1, 12, "newest")]
    [InlineData(100, 10, 1, 12, "newest")]
    [InlineData(0, 100, 0, 12, "newest")]
    [InlineData(0, 100, 1, 101, "newest")]
    [InlineData(0, 100, 1, 12, "unknown")]
    public void Validate_InvalidCriteria_RejectsRequest(
        decimal min, decimal max, int page, int size, string sort)
    {
        var query = new MarketplaceQuery(
            MinPrice: min, MaxPrice: max, Page: page, PageSize: size, SortBy: sort);

        Assert.False(new MarketplaceQueryValidator().Validate(query).IsValid);
    }

    [Fact]
    public void Validate_AllSupportedFilters_AcceptsRequest()
    {
        var query = new MarketplaceQuery("Gạo", 1, 2, "Sóc Trăng", 3, "VietGAP",
            0, 200000, 4, "rating", 2, 24);

        Assert.True(new MarketplaceQueryValidator().Validate(query).IsValid);
    }
}
