using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgriSustain.Application.Common;
using AgriSustain.Application.DTOs.Authentication;
using AgriSustain.Application.DTOs.Marketplace;
using AgriSustain.Infrastructure.Data.Contexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgriSustain.IntegrationTests;

public sealed class MarketplaceAndPasswordTests
{
    [SqlFact]
    public async Task BrowseAsync_PublicVisibilityAndFilters_UseSqlQueries()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        using var host = new SqlApiFactory(database.ConnectionString);
        using var client = host.CreateClient();

        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "database")))
        {
            root = Directory.GetParent(root)?.FullName
                ?? throw new DirectoryNotFoundException("Project database directory missing.");
        }
        string seed = await File.ReadAllTextAsync(
            Path.Combine(root, "database", "seed-marketplace-demo.sql"));
        await database.ExecuteAsync(seed);
        await database.ExecuteAsync(seed);
        Assert.Equal(7, await database.NumberAsync(
            "SELECT COUNT(*) FROM dbo.ProductImage "
            + "WHERE ImageUrl LIKE '/marketplace/%' AND IsPrimary=1"));

        var filters = await client.GetFromJsonAsync<MarketplaceFilters>("/api/products/filters");
        Assert.NotNull(filters);
        Assert.NotEmpty(filters.Categories);
        foreach (string sort in new[] { "newest", "price-asc", "price-desc", "rating" })
        {
            var result = await client.GetFromJsonAsync<PagedResponse<ProductSummary>>(
                $"/api/products?sortBy={sort}&pageSize=2");
            Assert.NotNull(result);
            Assert.True(result.Total >= 3);
            Assert.Equal(2, result.Items.Count);
            Assert.DoesNotContain(result.Items, item => item.Name.Contains("chưa công bố"));
        }

        var page = await client.GetFromJsonAsync<PagedResponse<ProductSummary>>("/api/products");
        ProductSummary product = Assert.IsType<ProductSummary>(page!.Items.First());
        var filtered = await client.GetFromJsonAsync<PagedResponse<ProductSummary>>(
            $"/api/products?categoryId={product.CategoryId}&supplierId={product.SupplierId}"
            + "&minPrice=" + product.Price.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        Assert.Contains(filtered!.Items, item => item.Id == product.Id);
        var empty = await client.GetFromJsonAsync<PagedResponse<ProductSummary>>(
            "/api/products?keyword=does-not-exist-12345");
        Assert.Empty(empty!.Items);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/products?minPrice=200&maxPrice=100")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync("/api/products?sortBy=invalid")).StatusCode);

        var detail = await client.GetFromJsonAsync<ProductDetail>($"/api/products/{product.Id}");
        Assert.Equal(product.Id, detail!.Product.Id);
        Assert.False(detail.CanAddToCart);
        long rice = await database.NumberAsync(
            "SELECT ProductId FROM dbo.Product WHERE ProductName=N'Gạo ST25'");
        var riceDetail = await client.GetFromJsonAsync<ProductDetail>($"/api/products/{rice}");
        Assert.Equal(4, riceDetail!.Images.Count);
        Assert.NotEmpty(riceDetail.Batches);
        Assert.Empty(riceDetail.Reviews);
        long hidden = await database.NumberAsync(
            "SELECT TOP(1) ProductId FROM dbo.Product WHERE IsPublished=0");
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/products/{hidden}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync("/api/products/99999999")).StatusCode);
        var invalid = await client.GetAsync("/api/products?categoryId=abc");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("MSG40", await invalid.Content.ReadAsStringAsync());
    }

    [SqlFact]
    public async Task ChangeAsync_BearerIdentity_UpdatesOnlyOwnPasswordAndSupportsLogin()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        using var host = new SqlApiFactory(database.ConnectionString);
        using var client = host.CreateClient();
        string email = await SetPasswordAsync(host, "OldPass123");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            "/api/passwords/change", new ChangePasswordRequest(
                "OldPass123", "NewPass123", "NewPass123"))).StatusCode);
        await LoginAsync(client, email, "OldPass123");

        var wrong = await client.PostAsJsonAsync("/api/passwords/change",
            new ChangePasswordRequest("wrong", "NewPass123", "NewPass123"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);
        Assert.Contains("MSG04", await wrong.Content.ReadAsStringAsync());
        var response = await client.PostAsJsonAsync("/api/passwords/change",
            new ChangePasswordRequest("OldPass123", "NewPass123", "NewPass123"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, await database.NumberAsync(
            "SELECT COUNT(*) FROM dbo.AuditLog WHERE Action='ChangePassword'"
            + " AND OldValue IS NULL AND NewValue IS NULL"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync(
            "/api/Authentication/login", new { email, password = "OldPass123" })).StatusCode);
        await LoginAsync(client, email, "NewPass123");
    }

    [SqlFact]
    public async Task ChangeAsync_AuditWriteFailure_RollsBackPassword()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        using var host = new SqlApiFactory(database.ConnectionString);
        using var client = host.CreateClient();
        string email = await SetPasswordAsync(host, "OldPass123");
        await LoginAsync(client, email, "OldPass123");
        await database.ExecuteAsync(
            "CREATE TRIGGER dbo.FailPasswordAudit ON dbo.AuditLog AFTER INSERT "
            + "AS THROW 51251,'Injected audit failure',1;");

        var response = await client.PostAsJsonAsync("/api/passwords/change",
            new ChangePasswordRequest("OldPass123", "NewPass123", "NewPass123"));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        await LoginAsync(client, email, "OldPass123");
        Assert.Equal(0, await database.NumberAsync(
            "SELECT COUNT(*) FROM dbo.AuditLog WHERE Action='ChangePassword'"));
    }

    [SqlFact]
    public async Task AddCartAsync_ValidPurchase_PersistsWithoutReservingStock()
    {
        await using var database = await SqlTestDatabase.CreateAsync();
        using var host = new SqlApiFactory(database.ConnectionString);
        using var client = host.CreateClient();
        string email = await SetPasswordAsync(host, "OldPass123");
        await LoginAsync(client, email, "OldPass123");
        var page = await client.GetFromJsonAsync<PagedResponse<ProductSummary>>("/api/products");
        ProductSummary product = page!.Items.First(item => item.AvailableQuantity > 10);
        await database.ExecuteAsync("DELETE FROM dbo.CartItem;");

        var response = await client.PostAsJsonAsync("/api/carts/current/items",
            new AddCartItemRequest(product.Id, 2));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var detail = await client.GetFromJsonAsync<ProductDetail>($"/api/products/{product.Id}");
        Assert.Equal(product.AvailableQuantity, detail!.Product.AvailableQuantity);
        Assert.Equal(2, await database.NumberAsync(
            $"SELECT SUM(Quantity) FROM dbo.CartItem WHERE ProductId={product.Id}"));
        var overStock = await client.PostAsJsonAsync("/api/carts/current/items",
            new AddCartItemRequest(product.Id, product.AvailableQuantity));
        Assert.Equal(HttpStatusCode.Conflict, overStock.StatusCode);
        var negative = await client.PostAsJsonAsync("/api/carts/current/items",
            new AddCartItemRequest(product.Id, -1));
        Assert.Equal(HttpStatusCode.BadRequest, negative.StatusCode);
    }

    private static async Task<string> SetPasswordAsync(SqlApiFactory host, string password)
    {
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AgriSustainDbContext>();
        var user = await db.User.SingleAsync(row => row.Username == "demo3uc_retail");
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 4);
        await db.SaveChangesAsync();
        return user.Email;
    }

    private static async Task LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync(
            "/api/Authentication/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var login = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", login.GetProperty("accessToken").GetString());
    }
}
