using GoGameShop.Api.Features.Baskets.GetBasket;
using GoGameShop.Api.Features.Baskets.UpsertBasket;

namespace GoGameShop.Api.Features.Baskets;

public static class BasketEndpoints
{
    public static void MapBaskets(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/baskets");
        group.MapUpsertBasket();
        group.MapGetBasket();
    }
}