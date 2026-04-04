using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Features.Games.GetGames;

public static class GetGamesEndpoint
{
    public static void MapGetGames(this IEndpointRouteBuilder app)
    {
        // GET /games
        app.MapGet(
            "/",
            async (GoGameShopContext dbContext) =>
                await dbContext
                    .Games.Include(game => game.Genre)
                    .Include(game => game.Rating)
                    .Select(game => new GameSummaryDto(
                        game.Id,
                        game.Name,
                        game.Genre!.Name,
                        game.Rating!.Name,
                        game.Price,
                        game.ReleaseDate
                    ))
                    .AsNoTracking()
                    .ToListAsync()
        );
    }
}
