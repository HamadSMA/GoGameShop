using System;
using GoGameShop.Api.Data;
using GoGameShop.Api.Models;

namespace GoGameShop.Api.Features.Genres;

public static class GetGenresEndpoint
{
    public static void MapGetGenres(this IEndpointRouteBuilder app)
    {
        app.MapGet("/genres", (GoGameShopData data) =>
        {
            var genres = data.GetGenres;

            return Results.Ok(genres.Select(genre => new GameGenres(
            genre.GenreId,
            genre.Name)));
        });
    }
}
