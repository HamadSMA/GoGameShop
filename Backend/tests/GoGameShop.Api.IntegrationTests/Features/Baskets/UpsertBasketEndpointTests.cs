using System.Net;
using System.Net.Http.Json;
using GoGameShop.Api.Features.Baskets.UpsertBasket;

namespace GoGameShop.Api.IntegrationTests.Features.Baskets;

public class UpsertBasketEndpointTests : IClassFixture<GoGameShopWebApplicationFactory>
{
    private readonly GoGameShopWebApplicationFactory _factory;

    public UpsertBasketEndpointTests(GoGameShopWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UpsertBasket_AuthenticatedOwner_Returns204()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());

        var dto = new UpsertBasketDto([]);

        // Act
        var response = await client.PutAsJsonAsync($"/baskets/{userId}", dto);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpsertBasket_AuthenticatedAsDifferentUser_Returns403()
    {
        // Arrange
        var basketUserId = Guid.NewGuid();
        var attackerUserId = Guid.NewGuid();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, attackerUserId.ToString());

        var dto = new UpsertBasketDto([]);

        // Act
        var response = await client.PutAsJsonAsync($"/baskets/{basketUserId}", dto);

        // Assert
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
