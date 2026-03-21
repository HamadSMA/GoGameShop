using System;
using GoGameShop.Api.Data;

namespace GoGameShop.Api.Features.Games.DeleteGame;

public static class DeleteGameEnpoint
{
    public static void MapDeleteGame(this IEndpointRouteBuilder app)
    {
        // DELETE /games/{id}
        app.MapDelete("/{id}", (Guid id, GoGameShopData data) =>
        {
            data.RemoveGame(id);

            return Results.NoContent();
        });

    }
}
