using System.Security.Claims;
using GoGameShop.Api.Features.Baskets.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Features.Baskets.GetBasket;

public static class GetBasketEndpoints
{
    public static void MapGetBasket(this IEndpointRouteBuilder app)
    {
        app.MapGet(
            "/{userId}",
            async (
                Guid userId,
                GoGameShopContext dbContext,
                IAuthorizationService authorizationService,
                ClaimsPrincipal user
            ) =>
            {
                if (userId == Guid.Empty)
                {
                    return Results.BadRequest();
                }

                var basket =
                    await dbContext
                        .Baskets.Include(basket => basket.Items)
                        .ThenInclude(basket => basket.Game)
                        .FirstOrDefaultAsync(basket => basket.Id == userId)
                    ?? new() { Id = userId };

                var authResult = await authorizationService.AuthorizeAsync(
                    user,
                    basket,
                    new OwnerOrAdminRequirement()
                );

                if (!authResult.Succeeded)
                {
                    return Results.Forbid();
                }

                var dto = new BasketDto(
                    basket.Id,
                    basket.Items.Select(item => new BasketItemDto(
                        item.GameId,
                        item.Game!.Name,
                        item.Game.Price,
                        item.Quantity,
                        item.Game.ImageUri
                    ))
                );
                return Results.Ok(dto);
            }
        );
    }
}
