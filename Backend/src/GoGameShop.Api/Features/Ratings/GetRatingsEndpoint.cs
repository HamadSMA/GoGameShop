namespace GoGameShop.Api.Features.Ratings;

public static class GetRatingsEndpoint
{
    public static void MapGetRatings(this IEndpointRouteBuilder app)
    {
        app.MapGet("/ratings", (GoGameShopData data) =>
        {
            var ratings = data.GetRatings;
            
            return Results.Ok(ratings.Select(rating => new GameRatingsDto(
                rating.RatingId,
                rating.Name
                )));

        });
    }
}
