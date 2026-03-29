namespace GoGameShop.Api.Features.Games.UpdateGame;

public static class UpdateGameEndpoint
{
    public static void MapUpdateGame(this IEndpointRouteBuilder app)
    {
        // PUT /games/{id}
        app.MapPut("/{id}", async (Guid id, GoGameShopContext dbContext, UpdateGameDto gameDto) =>
        {
            var game = await dbContext.Games.FindAsync(id);

            if (game is null)
            {
                return Results.NotFound("Game not found");
            }

            game.Name = gameDto.Name;
            game.GenreId = gameDto.GenreId;
            game.RatingId = gameDto.RatingId;
            game.ReleaseDate = gameDto.ReleaseDate;
            game.Price = gameDto.Price;
            game.Description = gameDto.Description;
            
            await dbContext.SaveChangesAsync();
            
            return Results.NoContent();

        });

    }
}
