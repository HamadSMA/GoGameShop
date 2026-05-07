using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace GoGameShop.Frontend.Auth;

public class CookieOidcRefresher(IOptionsMonitor<OpenIdConnectOptions> oidcOptions)
{
    public async Task ValidateOrRefreshCookieAsync(
        CookieValidatePrincipalContext context,
        string oidcScheme
    )
    {
        var expiresText = context.Properties.GetTokenValue("expires_at");
        if (!DateTimeOffset.TryParse(expiresText, out var expiresAt))
            return;

        if (DateTimeOffset.UtcNow < expiresAt - TimeSpan.FromMinutes(5))
            return;

        var opts = oidcOptions.Get(oidcScheme);
        var config = await opts.ConfigurationManager!.GetConfigurationAsync(
            context.HttpContext.RequestAborted
        );

        var tokenEndpoint =
            config.TokenEndpoint ?? throw new InvalidOperationException("Token endpoint missing.");

        using var response = await opts.Backchannel.PostAsync(
            tokenEndpoint,
            new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["client_id"] = opts.ClientId!,
                    ["client_secret"] = opts.ClientSecret!,
                    ["refresh_token"] = context.Properties.GetTokenValue("refresh_token")!,
                }
            ),
            context.HttpContext.RequestAborted
        );

        if (!response.IsSuccessStatusCode)
        {
            context.RejectPrincipal();
            return;
        }

        var json = await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted);
        var message = new OpenIdConnectMessage(json);

        var newExpiry =
            DateTimeOffset.UtcNow + TimeSpan.FromSeconds(double.Parse(message.ExpiresIn));
        context.Properties.UpdateTokenValue("access_token", message.AccessToken!);
        context.Properties.UpdateTokenValue("refresh_token", message.RefreshToken!);
        context.Properties.UpdateTokenValue("expires_at", newExpiry.ToString("o"));
        context.ShouldRenew = true;
    }
}
