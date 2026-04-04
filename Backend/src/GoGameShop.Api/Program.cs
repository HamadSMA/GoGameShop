using GoGameShop.Api.Shared.ErrorHandling;
using Microsoft.AspNetCore.HttpLogging;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("GoGameShop");

// builder.Services.AddExceptionHandler<GlobalErrorHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddSqlite<GoGameShopContext>(connectionString);
builder.Services.AddValidation();
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields =
        HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

var app = builder.Build();

// app.UseMiddleware<RequestTimingMiddleware>(); Kept for reference

app.MapGames();
app.MapGetGenres();
app.MapGetRatings();

app.UseHttpLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseStatusCodePages();

await app.InitializeDbAsync();

app.Run();
