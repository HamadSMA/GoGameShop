using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Features.Ratings;

public static class GetRatingsEndpoint
{
    public static void MapGetRatings(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ratings", (GoGameShopContext dbContext) =>

            dbContext.Ratings.Select(rating => new GameRatingsDto(
                rating.Id,
                rating.Name)).AsNoTracking());
    }
}
