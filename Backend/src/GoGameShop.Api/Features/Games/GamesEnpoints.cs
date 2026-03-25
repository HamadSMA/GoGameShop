namespace GoGameShop.Api.Features.Games;

public static class GamesEndpoints
{
    public static void MapGames(this IEndpointRouteBuilder app)
    {

        var games = app.MapGroup("/games");

        games.MapGetGames();
        games.MapGetGame();
        games.MapCreateGame();
        games.MapUpdateGame();
        games.MapDeleteGame();
    }
}
