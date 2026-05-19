using System.Security.Claims;
using GoGameShop.Api.Shared.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ClaimTypes = GoGameShop.Api.Shared.Authorization.ClaimTypes;

namespace GoGameShop.Api.UnitTests.Shared.Authorization;

public class KeycloakClaimsTransformerTests
{
    [Fact]
    public void Transform_MultipleScopesInOneClaim_SplitsIntoSeparateClaims()
    {
        // Arrange
        var transformer = new KeycloakClaimsTransformer(
            Substitute.For<ILogger<KeycloakClaimsTransformer>>()
        );

        var identity = new ClaimsIdentity(
            claims: [new Claim(ClaimTypes.Scope, "openid profile email")],
            authenticationType: "TestAuth"
        );

        var context = BuildContext(identity);

        // Act
        transformer.Transform(context);

        // Assert
        var scopes = identity.FindAll(ClaimTypes.Scope).Select(c => c.Value).ToList();
        Assert.Equal(["openid", "profile", "email"], scopes);
    }

    [Fact]
    public void Transform_MultipleScopes_RemovesOriginalCombinedClaim()
    {
        // Arrange
        var transformer = new KeycloakClaimsTransformer(
            Substitute.For<ILogger<KeycloakClaimsTransformer>>()
        );

        var identity = new ClaimsIdentity(
            claims: [new Claim(ClaimTypes.Scope, "openid profile email")],
            authenticationType: "TestAuth"
        );

        var context = BuildContext(identity);

        // Act
        transformer.Transform(context);

        // Assert
        Assert.DoesNotContain(identity.Claims, c => c.Value == "openid profile email");
    }

    [Fact]
    public void Transform_NoScopeClaim_LeavesIdentityUnchanged()
    {
        // Arrange
        var transformer = new KeycloakClaimsTransformer(
            Substitute.For<ILogger<KeycloakClaimsTransformer>>()
        );

        var identity = new ClaimsIdentity(
            claims: [new Claim("name", "test-user")],
            authenticationType: "TestAuth"
        );

        var claimsBefore = identity.Claims.Select(c => (c.Type, c.Value)).ToList();
        var context = BuildContext(identity);

        // Act
        transformer.Transform(context);

        // Assert
        var claimsAfter = identity.Claims.Select(c => (c.Type, c.Value)).ToList();
        Assert.Equal(claimsBefore, claimsAfter);
    }

    [Fact]
    public void Transform_SingleScope_ProducesOneClaim()
    {
        // Arrange
        var transformer = new KeycloakClaimsTransformer(
            Substitute.For<ILogger<KeycloakClaimsTransformer>>()
        );

        var identity = new ClaimsIdentity(
            claims: [new Claim(ClaimTypes.Scope, "openid")],
            authenticationType: "TestAuth"
        );

        var context = BuildContext(identity);

        // Act
        transformer.Transform(context);

        // Assert
        var scopes = identity.FindAll(ClaimTypes.Scope).Select(c => c.Value).ToList();
        Assert.Equal(["openid"], scopes);
    }

    private static TokenValidatedContext BuildContext(ClaimsIdentity identity)
    {
        return new TokenValidatedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme("Test", "Test", typeof(JwtBearerHandler)),
            new JwtBearerOptions()
        )
        {
            Principal = new ClaimsPrincipal(identity)
        };
    }

    [Fact]
    public void Transform_PrincipalIsNull_DoesNotThrow()
    {
        // Arrange
        var transformer = new KeycloakClaimsTransformer(
            Substitute.For<ILogger<KeycloakClaimsTransformer>>()
        );

        var context = new TokenValidatedContext(
            new DefaultHttpContext(),
            new AuthenticationScheme("Test", "Test", typeof(JwtBearerHandler)),
            new JwtBearerOptions()
        )
        {
            Principal = null
        };

        // Act
        var exception = Record.Exception(() => transformer.Transform(context));

        // Assert
        Assert.Null(exception);
    }
}
