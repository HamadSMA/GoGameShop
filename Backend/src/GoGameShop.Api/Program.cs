using System.Reflection.Metadata;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("GoGameShop");
builder.Services.AddSqlite<GoGameShopContext>(connectionString);

builder.Services.AddValidation();
builder.Services.AddSingleton<GoGameShopData>();

var app = builder.Build();


app.MapGames();
app.MapGetGenres();
app.MapGetRatings();
app.Run();
