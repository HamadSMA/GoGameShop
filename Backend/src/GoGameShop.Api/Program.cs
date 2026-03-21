var builder = WebApplication.CreateBuilder(args);

builder.Services.AddValidation();
builder.Services.AddSingleton<GoGameShopData>();

var app = builder.Build();


app.MapGames();
app.MapGetGenres();
app.MapGetRatings();
app.Run();
