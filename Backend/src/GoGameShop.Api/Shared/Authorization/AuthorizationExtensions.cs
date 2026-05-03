namespace GoGameShop.Api.Shared.Authorization;

public static class AuthorizationExtensions
{
    private const string ApiAccessScope = "gogameshop_api.all";

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
                    authBuilder.RequireClaim(ClaimTypes.Scope, ApiAccessScope);
                }
            )
            .AddPolicy(
                Policies.AdminAccess,
                authBuilder =>
                {
                    authBuilder.RequireClaim(ClaimTypes.Scope, ApiAccessScope);
                    authBuilder.RequireRole(Roles.Admin);
                }
            );
        return builder;
    }
}
