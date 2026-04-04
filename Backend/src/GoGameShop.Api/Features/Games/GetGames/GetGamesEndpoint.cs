using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Features.Games.GetGames;

public static class GetGamesEndpoint
{
    public static void MapGetGames(this IEndpointRouteBuilder app)
    {
        // GET /games
        app.MapGet(
            "/",
            async (GoGameShopContext dbContext, [AsParameters] GetGamesDto request) =>
            {
                var skipCount = (request.PageNumber - 1) * request.PageSize;

                var gamesOnPage = await dbContext.Games
                    .OrderBy(game => game.Name)
                    .Skip(skipCount)
                    .Take(request.PageSize)
                    .Include(game => game.Genre)
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
                    .ToListAsync();

                var totalGames = await dbContext.Games.CountAsync();
                var totalPages = (int)Math.Ceiling(totalGames / (double)request.PageSize);

                return new GamesPageDto(totalPages, gamesOnPage);
            }
        );
    }
}