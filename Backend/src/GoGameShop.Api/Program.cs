using GoGameShop.Api.Features.Baskets;
using GoGameShop.Api.Features.Baskets.Authorization;
using GoGameShop.Api.Shared.Authorization;
using GoGameShop.Api.Shared.FileUpload;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpLogging;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("GoGameShop");

builder.Services.AddSqlite<GoGameShopContext>(connectionString);
builder.Services.AddValidation();

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

// This is an extension method at /Shared/Authorization
builder.AddGoGameShopAuthorization();

// builder.Services.AddExceptionHandler<GlobalErrorHandler>(); Kept for reference

builder.Services.AddOpenApi();

builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<FileUploader>();

// Authentication and Authorization service add its middleware automatically, no need to add it to the pipeline unless it creates conflict, then it must be added manually in the pipeline

builder
    .Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = "role";
    });
builder.Services.AddSingleton<IAuthorizationHandler, BasketAuthorizationHandler>();

var app = builder.Build();

// app.UseMiddleware<RequestTimingMiddleware>(); Kept for reference

app.UseStaticFiles();

// ASP.NET core uses the middleware for authorization automatically, however, in this case we need to explicitly defince it because we need it to be after app.UseStaticFiles() so that static files render for anonymous users.
app.UseAuthorization();

app.MapGames();
app.MapGetGenres();
app.MapGetRatings();
app.MapBaskets();

app.UseHttpLogging();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();
else
    app.UseExceptionHandler();

await app.InitializeDbAsync();

app.Run();
