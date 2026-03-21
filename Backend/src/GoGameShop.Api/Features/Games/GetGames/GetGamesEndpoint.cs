namespace GoGameShop.Api.Features.Games.GetGames;

public static class GetGamesEndpoint
{
    public static void MapGetGames(this IEndpointRouteBuilder app)
    {
        // GET /games
        app.MapGet("/", (GoGameShopData data) => data.GetGames.Select(game => new GameSummaryDto(
            game.Id,
            game.Name,
            game.GenreId,
            game.RatingId,
            game.Price,
            game.ReleaseDate
        )));
    }
}
