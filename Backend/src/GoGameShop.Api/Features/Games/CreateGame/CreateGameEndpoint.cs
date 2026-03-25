namespace GoGameShop.Api.Features.Games.CreateGame;

public static class CreateGameEndpoint
{
    public static void MapCreateGame(this IEndpointRouteBuilder app)
    {
        // POST /games
        app.MapPost("/", (GoGameShopData data, CreateGameDto gameDto) =>
        {
            var genre = data.GetGenre(gameDto.GenreId);
            var rating = data.GetRating(gameDto.RatingId);
            if (genre is null)
            {
                return Results.BadRequest("Invalid genre id");
            }

            if (rating is null)
            {
                return Results.BadRequest("Invalid genre id");
            }


            Game game = new()
            {
                Name = gameDto.Name,
                Genre = genre,
                GenreId = genre.Id,
                Rating = rating,
                RatingId = rating.Id,
                ReleaseDate = gameDto.ReleaseDate,
                Price = gameDto.Price,
                Description = gameDto.Description
            };

            data.AddGame(game);
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
