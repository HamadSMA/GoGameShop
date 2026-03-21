using GoGameShop.Api.Features.Games.GetGame;
using GoGameShop.Api.Features.Games.GetGames;
using GoGameShop.Api.Data;
using GoGameShop.Api.Features.Games;
using GoGameShop.Api.Features.Genres;
using GoGameShop.Api.Features.Ratings;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidation();
builder.Services.AddSingleton<GoGameShopData>();

var app = builder.Build();


app.MapGames();
app.MapGetGenres();
app.MapGetRatings();
app.Run();
