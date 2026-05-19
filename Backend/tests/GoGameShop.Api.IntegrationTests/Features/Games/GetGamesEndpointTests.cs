using System.Net;
using System.Net.Http.Json;
using GoGameShop.Api.Features.Games.GetGames;

namespace GoGameShop.Api.IntegrationTests.Features.Games;

public class GetGamesEndpointTests : IClassFixture<GoGameShopWebApplicationFactory>
{
    private readonly HttpClient _client;

    public GetGamesEndpointTests(GoGameShopWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetGames_Anonymous_Returns200AndNonEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/games");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<GamesPageDto>();
        Assert.NotNull(page);
        Assert.NotEmpty(page.Games);
    }
}
