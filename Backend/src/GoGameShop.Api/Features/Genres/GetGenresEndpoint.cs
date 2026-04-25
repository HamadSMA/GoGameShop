using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Features.Genres;

public static class GetGenresEndpoint
{
    public static void MapGetGenres(this IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/genres",
                async (GoGameShopContext dbContext) =>
                    await dbContext
                        .Genres.Select(genre => new GameGenresDto(genre.Id, genre.Name))
                        .AsNoTracking()
                        .ToListAsync()
            )
            .AllowAnonymous();
    }
}
