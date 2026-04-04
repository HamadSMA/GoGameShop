namespace GoGameShop.Api.Features.Games.CreateGame;

public static class CreateGameEndpoint
{
    public static void MapCreateGame(this IEndpointRouteBuilder app)
    {
        // POST /games
        app.MapPost(
            "/",
            async (GoGameShopContext dbContext, CreateGameDto gameDto, ILogger<Program> logger) =>
            {
                Game game =
                    new()
                    {
                        Name = gameDto.Name,
                        GenreId = gameDto.GenreId,
                        RatingId = gameDto.RatingId,
                        ReleaseDate = gameDto.ReleaseDate,
                        Price = gameDto.Price,
                        Description = gameDto.Description
                    };

                dbContext.Add(game);
                await dbContext.SaveChangesAsync();

                logger.LogInformation(
                    "Created Game {GameName} with price {GamePrice}",
                    game.Name,
                    game.Price
                );

                return Results.CreatedAtRoute(
                    EndpointNames.GetGame,
                    new { id = game.Id },
                    new GameDetailsDto(
                        game.Id,
                        game.Name,
                        game.GenreId,
                        game.RatingId,
                        game.ReleaseDate,
                        game.Price,
                        game.Description
                    )
                );
            }
        );
    }
}
