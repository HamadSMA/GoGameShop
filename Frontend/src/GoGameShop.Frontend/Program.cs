using GoGameShop.Frontend.Auth;
using GoGameShop.Frontend.Clients;
using GoGameShop.Frontend.Components;
using GoGameShop.Frontend.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents();

var apiBase = builder.Configuration["ApiBaseUrl"]!;
var kc = builder.Configuration.GetSection("Keycloak");

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<BasketState>();
builder.Services.AddSingleton<CookieOidcRefresher>();

builder.Services.AddTransient<ApiAuthorizationHandler>();

builder
    .Services.AddHttpClient<GamesClient>(c => c.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<ApiAuthorizationHandler>();
builder
    .Services.AddHttpClient<LookupClient>(c => c.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<ApiAuthorizationHandler>();
builder
    .Services.AddHttpClient<ServerBasketClient>(c => c.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<ApiAuthorizationHandler>();

builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login";
        options.Events.OnValidatePrincipal = async context =>
        {
            var refresher =
                context.HttpContext.RequestServices.GetRequiredService<CookieOidcRefresher>();
            await refresher.ValidateOrRefreshCookieAsync(
                context,
                OpenIdConnectDefaults.AuthenticationScheme
            );
        };
    })
    .AddOpenIdConnect(options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.MetadataAddress = kc["MetadataAddress"]!;
        options.ClientId = kc["ClientId"];
        options.ClientSecret = kc["ClientSecret"];
        options.ResponseType = OpenIdConnectResponseType.Code;
        options.RequireHttpsMetadata = false;
        options.SaveTokens = true;
        options.MapInboundClaims = false;
        options.Scope.Clear();
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("gogameshop_api.all");
        options.TokenValidationParameters.RoleClaimType = "role";
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapGet(
    "/login",
    () =>
        Results.Challenge(
            new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = "/" },
            [OpenIdConnectDefaults.AuthenticationScheme]
        )
);

app.MapPost(
        "/logout",
        async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            await ctx.SignOutAsync(
                OpenIdConnectDefaults.AuthenticationScheme,
                new Microsoft.AspNetCore.Authentication.AuthenticationProperties
                {
                    RedirectUri = "/"
                }
            );
        }
    )
    .RequireAuthorization();

app.MapRazorComponents<App>();

app.Run();
