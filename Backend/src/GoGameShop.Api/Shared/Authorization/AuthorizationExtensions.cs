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
                    authBuilder.RequireClaim("scope", ApiAccessScope);
                }
            )
            .AddPolicy(
                Policies.AdminAccess,
                authBuilder =>
                {
                    authBuilder.RequireClaim("scope", ApiAccessScope);
                    authBuilder.RequireRole(Roles.Admin);
                }
            );
        return builder;
    }
}
