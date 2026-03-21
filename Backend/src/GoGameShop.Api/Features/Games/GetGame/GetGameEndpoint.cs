using System;
using GoGameShop.Api.Models;
using GoGameShop.Api.Data;

namespace GoGameShop.Api.Features.Games.GetGame;

public static class GetGameEndpoint
{
    public static void MapGetGame(this IEndpointRouteBuilder app)
    {
        // GET /games/{id}
        app.MapGet("/{id}", (Guid id, GoGameShopData data) =>
        {
            Game? game = data.GetGame(id);
            return game is null ? Results.NotFound() : Results.Ok(
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
        }).WithName("GetGame");
    }
}
