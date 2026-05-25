using System;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace GoGameShop.Api.Shared.Authorization;

public class EntraClaimsTransformer(ILogger<EntraClaimsTransformer> logger)
{
    private const string ScopeClaimType = "scp";
    private const string OidClaimType = "oid";

    public void Transform(TokenValidatedContext context)
    {
        var identity = context.Principal?.Identity as ClaimsIdentity;

        identity?.TransformScopeClaim(ScopeClaimType);
        identity?.MapUserIdClaim(OidClaimType);
        context.Principal?.LogAllClaims(logger);
    }
}
