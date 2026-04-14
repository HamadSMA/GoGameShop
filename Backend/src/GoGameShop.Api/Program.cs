using GoGameShop.Api.Shared.FileUpload;
using Microsoft.AspNetCore.HttpLogging;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("GoGameShop");

builder.Services.AddSqlite<GoGameShopContext>(connectionString);
builder.Services.AddValidation();

// builder.Services.AddExceptionHandler<GlobalErrorHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields =
        HttpLoggingFields.RequestMethod
        | HttpLoggingFields.RequestPath
        | HttpLoggingFields.ResponseStatusCode
        | HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<FileUploader>();

var app = builder.Build();

// app.UseMiddleware<RequestTimingMiddleware>(); Kept for reference


app.MapGames();
app.MapGetGenres();
app.MapGetRatings();

app.UseStaticFiles();

app.UseHttpLogging();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();
else
    app.UseExceptionHandler();

await app.InitializeDbAsync();

app.Run();