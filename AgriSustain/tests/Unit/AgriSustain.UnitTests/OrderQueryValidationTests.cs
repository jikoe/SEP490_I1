using AgriSustain.Application.DTOs.Orders;
using AgriSustain.Application.Validators.Orders;
using Xunit;

namespace AgriSustain.UnitTests;

public sealed class OrderQueryValidationTests
{
    private readonly OrderQueryValidator _validator = new();

    [Fact]
    public async Task OrderQuery_ValidInputs_PassesValidation()
    {
        var query = new OrderQuery(Status: "Completed", Keyword: "ORD-2026", Page: 1, PageSize: 10);
        var result = await _validator.ValidateAsync(query);
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(0, 10)] // Page < 1
    [InlineData(-1, 10)]
    [InlineData(1, 0)] // PageSize < 1
    [InlineData(1, 101)] // PageSize > 100
    public async Task OrderQuery_InvalidInputs_FailsValidation(int page, int pageSize)
    {
        var query = new OrderQuery(Status: null, Keyword: null, Page: page, PageSize: pageSize);
        var result = await _validator.ValidateAsync(query);
        Assert.False(result.IsValid);
    }
}
