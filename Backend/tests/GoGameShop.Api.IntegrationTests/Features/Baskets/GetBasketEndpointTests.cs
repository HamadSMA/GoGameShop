using System.Net;

namespace GoGameShop.Api.IntegrationTests.Features.Baskets;

public class GetBasketEndpointTests : IClassFixture<GoGameShopWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GetBasketEndpointTests(GoGameShopWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetBasket_NoAuth_Returns401()
    {
        // Arrange
        var anyUserId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/baskets/{anyUserId}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
