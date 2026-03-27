using Microsoft.EntityFrameworkCore;

namespace GoGameShop.Api.Features.Games.DeleteGame;

public static class DeleteGameEndpoint
{
    public static void MapDeleteGame(this IEndpointRouteBuilder app)
    {
        // DELETE /games/{id}
        app.MapDelete("/{id}", (Guid id, GoGameShopContext dbContext) =>
        {
            dbContext.Games
                .Where(game => game.Id == id)
                .ExecuteDelete();
            
            return Results.NoContent();
        });

    }
}
