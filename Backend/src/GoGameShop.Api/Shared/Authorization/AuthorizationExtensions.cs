using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Net.Http.Headers;

namespace GoGameShop.Api.Shared.Authorization;

public static class AuthorizationExtensions
{
    // Role vs Roles
    // This is not a bug. Roles is used for Entra. Role is for Keycloak
    private const string ApiAccessScope = "gogameshop_api.all";

    public static IHostApplicationBuilder AddGoGameShopAuthentication(
        this IHostApplicationBuilder builder
    )
    {
        var authBuilder = builder.Services.AddAuthentication(Schemes.KeycloakOrEntra);

        if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSingleton<KeycloakClaimsTransformer>();
            authBuilder
                .AddJwtBearer(options =>
                {
                    options.MapInboundClaims = false;
                    options.TokenValidationParameters.RoleClaimType = GoGameShopClaimTypes.Role;
                })
                .AddJwtBearer(
                    Schemes.Keycloak,
                    options =>
                    {
                        options.MapInboundClaims = false;
                        options.TokenValidationParameters.RoleClaimType = GoGameShopClaimTypes.Role;
                        options.RequireHttpsMetadata = false;
                        options.Events = new JwtBearerEvents()
                        {
                            OnTokenValidated = context =>
                            {
                                var transformer =
                                    context.HttpContext.RequestServices.GetRequiredService<KeycloakClaimsTransformer>();

                                transformer.Transform(context);

                                return Task.CompletedTask;
                            }
                        };
                    }
                );
        }

        builder.Services.AddSingleton<EntraClaimsTransformer>();

        authBuilder.AddJwtBearer(
            Schemes.Entra,
            options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters.RoleClaimType = GoGameShopClaimTypes.Roles;
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = context =>
                    {
                        var transformer =
                            context.HttpContext.RequestServices.GetRequiredService<EntraClaimsTransformer>();
                        transformer.Transform(context);
                        return Task.CompletedTask;
                    }
                };
            }
        );

        authBuilder.AddPolicyScheme(
            Schemes.KeycloakOrEntra,
            Schemes.KeycloakOrEntra,
            options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    string authorization = context.Request.Headers[HeaderNames.Authorization]!;

                    if (!string.IsNullOrEmpty(authorization) && authorization.StartsWith("Bearer "))
                    {
                        var token = authorization["Bearer ".Length..].Trim();

                        var jwtHandler = new JwtSecurityTokenHandler();

                        return
                            jwtHandler.CanReadToken(token)
                            && jwtHandler.ReadJwtToken(token).Issuer.Contains("ciamlogin.com")
                            ? Schemes.Entra
                            : Schemes.Keycloak;
                    }

                    return Schemes.Entra;
                };
            }
        );
        return builder;
    }

    public static IHostApplicationBuilder AddGoGameShopAuthorization(
        this IHostApplicationBuilder builder
    )
    {
        builder
            .Services.AddAuthorizationBuilder()
            .AddFallbackPolicy(
                Policies.UserAccess,
                authBuilder =>
                {
                    authBuilder.RequireClaim(GoGameShopClaimTypes.Scope, ApiAccessScope);
                }
            )
            .AddPolicy(
                Policies.AdminAccess,
                authBuilder =>
                {
                    authBuilder.RequireClaim(GoGameShopClaimTypes.Scope, ApiAccessScope);
                    authBuilder.RequireRole(Roles.Admin);
                }
            );
        return builder;
    }
}
