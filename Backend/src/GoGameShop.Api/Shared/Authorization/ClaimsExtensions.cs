using System;
using System.Runtime.CompilerServices;
using System.Security.Claims;

namespace GoGameShop.Api.Shared.Authorization;

public static class ClaimsExtensions
{
    public static void TransformScopeClaim(this ClaimsIdentity? identity, string sourceScopeClaim)
    {
        var scopeClaim = identity?.FindFirst(sourceScopeClaim);

        if (scopeClaim is null)
        {
            return;
        }

        var scopes = scopeClaim.Value.Split(' ');
        identity?.RemoveClaim(scopeClaim);

        identity?.AddClaims(scopes.Select(scope => new Claim(GoGameShopClaimTypes.Scope, scope)));
    }

    public static void MapUserIdClaim(this ClaimsIdentity? identity, string sourceClaimType)
    {
        var sourceClaim = identity?.FindFirst(sourceClaimType);

        if (sourceClaim is not null)
        {
            identity?.AddClaim(new Claim(GoGameShopClaimTypes.UserId, sourceClaim.Value));
        }
    }

    public static void LogAllClaims(this ClaimsPrincipal? principal, ILogger logger)
    {
        var claims = principal?.Claims;
        if (claims is null)
        {
            return;
        }

        foreach (var claim in claims)
        {
            logger.LogTrace("Claim: {ClaimType}, Value: {ClaimValue}", claim.Type, claim.Value);
        }
    }
}
