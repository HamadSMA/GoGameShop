using System.Diagnostics;
using Microsoft.Data.Sqlite;

namespace GoGameShop.Api.Features.Games.GetGame;

public static class GetGameEndpoint
{
    public static void MapGetGame(this IEndpointRouteBuilder app)
    {
        // GET /games/{id}
        app.MapGet(
                "/{id}",
                async (Guid id, GoGameShopContext dbContext, ILogger<Program> logger) =>
                {
                    var game = await dbContext.Games.FindAsync(id);
                    return game is null
                        ? Results.NotFound()
                        : Results.Ok(
                            new GameDetailsDto(
                                game.Id,
                                game.Name,
                                game.GenreId,
                                game.RatingId,
                                game.Price,
                                game.ReleaseDate,
                                game.Description
                            )
                        );
                }
            )
            .WithName(EndpointNames.GetGame);
    }
}
