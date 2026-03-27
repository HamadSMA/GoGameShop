using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Features.Genres;

public static class GetGenresEndpoint
{
    public static void MapGetGenres(this IEndpointRouteBuilder app)
    {
        app.MapGet("/genres", (GoGameShopContext dbContext) =>

            dbContext.Genres.Select(genre => new GameGenresDto(
                genre.Id,
                genre.Name)).AsNoTracking());
    }
}
