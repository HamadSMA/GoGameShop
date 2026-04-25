using GoGameShop.Api.Features.Baskets;
using GoGameShop.Api.Shared.Authorization;
using GoGameShop.Api.Shared.FileUpload;
using Microsoft.AspNetCore.HttpLogging;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("GoGameShop");

builder.Services.AddSqlite<GoGameShopContext>(connectionString);
builder.Services.AddValidation();

// Authentication service adds its middleware automatically, no need to add it to the pipeline.
builder
    .Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = "role";
    });

// Authorization service adds its middleware automatically, no need to add it to the pipeline.
builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy(
        Policies.UserAccess,
        authBuilder =>
        {
            authBuilder.RequireClaim("scope", "gogameshop_api.all");
        }
    )
    .AddPolicy(
        Policies.AdminAccess,
        authBuilder =>
        {
            authBuilder.RequireClaim("scope", "gogameshop_api.all");
            authBuilder.RequireRole(Roles.Admin);
        }
    );

// builder.Services.AddExceptionHandler<GlobalErrorHandler>(); Kept for reference
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
app.MapBaskets();

app.UseStaticFiles();

app.UseHttpLogging();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();
else
    app.UseExceptionHandler();

await app.InitializeDbAsync();

app.Run();
