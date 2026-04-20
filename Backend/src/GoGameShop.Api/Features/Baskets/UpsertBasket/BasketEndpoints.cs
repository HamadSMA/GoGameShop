

namespace GoGameShop.Api.Features.Baskets.UpsertBasket;


public static class BasketEndpoints
{
    public static void MapBaskets(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/baskets");
        group.MapUpsertBasket();
    }
}