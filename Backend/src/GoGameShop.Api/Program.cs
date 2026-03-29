var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("GoGameShop");

builder.Services.AddSqlite<GoGameShopContext>(connectionString);
builder.Services.AddValidation();

var app = builder.Build();


app.MapGames();
app.MapGetGenres();
app.MapGetRatings();

await app.InitializeDbAsync();

app.Run();
