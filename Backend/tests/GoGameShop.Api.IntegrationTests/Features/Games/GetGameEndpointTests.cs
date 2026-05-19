using System.Net;

namespace GoGameShop.Api.IntegrationTests.Features.Games;

public class GetGameEndpointTests : IClassFixture<GoGameShopWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GetGameEndpointTests(GoGameShopWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetGame_NonExistentId_Returns404()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/games/{nonExistentId}");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
