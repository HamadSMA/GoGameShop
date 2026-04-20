using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Features.Baskets.UpsertBasket;

public static class UpsertBasketEndpoint
{
    public static void MapUpsertBasket(this IEndpointRouteBuilder app)
    {
        // PUT /baskets/{id}
        app.MapPut("/{userId}", async 
        (Guid userId, UpsertBasketDto upsertBasketDto, GoGameShopContext dbContext) =>
        {
            var basket = await dbContext.Baskets
                                        .Include(basket => basket.Items)
                                        .FirstOrDefaultAsync(basket => basket.Id == userId);

            if (basket is null)
            {
                basket = new CustomerBasket
                {
                    Id = userId,
                    Items = upsertBasketDto.Items.Select(item => new BasketItem
                    {
                        GameId = item.GameId,
                        Quantity = item.Quantity
                    }).ToList()
                };
                dbContext.Baskets.Add(basket);
                
            }
            else
            {
                basket.Items = upsertBasketDto.Items.Select(item => new BasketItem
                {
                    GameId = item.GameId,
                    Quantity = item.Quantity
                }).ToList();
            }
            await dbContext.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}