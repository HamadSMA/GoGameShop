using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectClaimTypes = GoGameShop.Api.Shared.Authorization.ClaimTypes;
using SystemClaimTypes = System.Security.Claims.ClaimTypes;

namespace GoGameShop.Api.IntegrationTests;

public class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder
) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "X-Test-User-Id";
    public const string RolesHeader = "X-Test-User-Roles";
    private const string ApiAccessScope = "gogameshop_api.all";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userIdValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userIdValues.ToString()),
            new(ProjectClaimTypes.Scope, ApiAccessScope)
        };

        if (Request.Headers.TryGetValue(RolesHeader, out var roleValues))
        {
            foreach (var role in roleValues.ToString().Split(','))
            {
                claims.Add(new Claim(ProjectClaimTypes.Role, role.Trim()));
            }
        }

        var identity = new ClaimsIdentity(
            claims,
            authenticationType: SchemeName,
            nameType: SystemClaimTypes.Name,
            roleType: ProjectClaimTypes.Role
        );

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
