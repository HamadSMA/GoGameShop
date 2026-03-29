namespace GoGameShop.Api.Features.Games.CreateGame;

public static class CreateGameEndpoint
{
    public static void MapCreateGame(this IEndpointRouteBuilder app)
    {
        // POST /games
        app.MapPost("/", async (GoGameShopContext dbContext, CreateGameDto gameDto) =>
        {
            
            Game game = new()
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
            
            return Results.CreatedAtRoute(EndpointNames.GetGame, new { id = game.Id }, new GameDetailsDto(
                game.Id,
                game.Name,
                game.GenreId,
                game.RatingId,
                game.ReleaseDate,
                game.Price,
                game.Description
            ));
        });
    }
}
