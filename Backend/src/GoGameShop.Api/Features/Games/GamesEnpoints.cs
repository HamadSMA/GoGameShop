using System;
using GoGameShop.Api.Features.Games.CreateGame;
using GoGameShop.Api.Features.Games.DeleteGame;
using GoGameShop.Api.Features.Games.GetGame;
using GoGameShop.Api.Features.Games.GetGames;
using GoGameShop.Api.Features.Games.UpdateGame;
using Microsoft.VisualBasic;

namespace GoGameShop.Api.Features.Games;

public static class GamesEnpoints
{
    public static void MapGames(this IEndpointRouteBuilder app)
    {

        var games = app.MapGroup("/games");

        games.MapGetGames();
        games.MapGetGame();
        games.MapCreateGame();
        games.MapUpdateGame();
        games.MapDeleteGame();
    }
}
