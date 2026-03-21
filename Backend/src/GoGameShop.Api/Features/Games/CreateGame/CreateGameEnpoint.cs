using System;
using GoGameShop.Api.Data;
using GoGameShop.Api.Features.Games.GetGame;
using GoGameShop.Api.Features.Games.Constants;
using GoGameShop.Api.Models;


namespace GoGameShop.Api.Features.Games.CreateGame;

public static class CreateGameEnpoint
{
    public static void MapCreateGame(this IEndpointRouteBuilder app)
    {
        // POST /games
        app.MapPost("/", (GoGameShopData data, CreateGameDto gameDto) =>
        {
            Genre? genre = data.GetGenre(gameDto.GenreId);
            Rating? rating = data.GetRating(gameDto.RatingId);
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
                GenreId = gameDto.GenreId,
                Rating = rating,
                RatingId = gameDto.RatingId,
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
                game.Price,
                game.ReleaseDate,
                game.Description
            ));
        });
    }
}
