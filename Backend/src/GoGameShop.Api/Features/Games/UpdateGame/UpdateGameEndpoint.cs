using System;
using GoGameShop.Api.Data;
using GoGameShop.Api.Models;

namespace GoGameShop.Api.Features.Games.UpdateGame;

public static class UpdateGameEndpoint
{
    public static void MapUpdateGame(this IEndpointRouteBuilder app)
    {
        // PUT /games/{id}
        app.MapPut("/{id}", (Guid id, GoGameShopData data, UpdateGameDto gameDto) =>
        {
            Game? game = data.GetGame(id);
            Genre? genre = data.GetGenre(gameDto.GenreId);
            Rating? rating = data.GetRating(gameDto.RatingId);

            if (game is null)
            {
                return Results.NotFound("Game not found");
            }

            if (genre is null)
            {
                return Results.BadRequest("Invalid genre id");
            }

            if (rating is null)
            {
                return Results.BadRequest("invalid rating id");
            }

            game.Name = gameDto.Name;
            game.Genre = genre;
            game.GenreId = genre.Id;
            game.Rating = rating;
            game.RatingId = rating.Id;
            game.ReleaseDate = gameDto.ReleaseDate;
            game.Price = gameDto.Price;
            game.Description = gameDto.Description;

            return Results.NoContent();
        });
    }
}
