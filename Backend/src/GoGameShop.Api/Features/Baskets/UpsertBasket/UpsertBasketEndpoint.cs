using System.Security.Claims;
using GoGameShop.Api.Features.Baskets.Authorization;
using GoGameShop.Api.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Features.Baskets.UpsertBasket;

public static class UpsertBasketEndpoint
{
    public static void MapUpsertBasket(this IEndpointRouteBuilder app)
    {
        // PUT /baskets/{id}
        app.MapPut(
            "/{userId}",
            async (
                Guid userId,
                UpsertBasketDto upsertBasketDto,
                GoGameShopContext dbContext,
                IAuthorizationService authorizationService,
                ClaimsPrincipal user
            ) =>
            {
                var basket = await dbContext
                    .Baskets.Include(basket => basket.Items)
                    .FirstOrDefaultAsync(basket => basket.Id == userId);

                if (basket is null)
                {
                    basket = new CustomerBasket
                    {
                        Id = userId,
                        Items = upsertBasketDto
                            .Items.Select(item => new BasketItem
                            {
                                GameId = item.GameId,
                                Quantity = item.Quantity
                            })
                            .ToList()
                    };
                    dbContext.Baskets.Add(basket);
                }
                else
                {
                    basket.Items = upsertBasketDto
                        .Items.Select(item => new BasketItem
                        {
                            GameId = item.GameId,
                            Quantity = item.Quantity
                        })
                        .ToList();
                }

                var authResult = await authorizationService.AuthorizeAsync(
                    user,
                    basket,
                    new OwnerOrAdminRequirement()
                );

                if (!authResult.Succeeded)
                {
                    return Results.Forbid();
                }

                await dbContext.SaveChangesAsync();
                return Results.NoContent();
            }
        );
    }
}
