using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GoGameShop.Api.Features.Baskets.Authorization;
using GoGameShop.Api.Models;
using GoGameShop.Api.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace GoGameShop.Api.UnitTests.Features.Baskets.Authorization;

public class BasketAuthorizationHandlerTests
{
    [Fact]
    public async Task HandleRequirement_UserIsBasketOwner_Succeeds()
    {
        // Arrange
        var basketId = Guid.NewGuid();
        var basket = new CustomerBasket { Id = basketId };

        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                claims: [new Claim(JwtRegisteredClaimNames.Sub, basketId.ToString())],
                authenticationType: "TestAuth"
            )
        );

        var requirement = new OwnerOrAdminRequirement();
        var context = new AuthorizationHandlerContext(
            requirements: [requirement],
            user: user,
            resource: basket
        );

        var handler = new BasketAuthorizationHandler();

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirement_UserIsAdmin_Succeeds()
    {
        // Arrange
        var basket = new CustomerBasket { Id = Guid.NewGuid() };
        var differentUserId = Guid.NewGuid();

        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                claims:
                [
                    new Claim(JwtRegisteredClaimNames.Sub, differentUserId.ToString()),
                    new Claim(System.Security.Claims.ClaimTypes.Role, Roles.Admin)
                ],
                authenticationType: "TestAuth"
            )
        );

        var requirement = new OwnerOrAdminRequirement();
        var context = new AuthorizationHandlerContext(
            requirements: [requirement],
            user: user,
            resource: basket
        );

        var handler = new BasketAuthorizationHandler();

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirement_UserIsNeitherOwnerNorAdmin_DoesNotSucceed()
    {
        // Arrange
        var basket = new CustomerBasket { Id = Guid.NewGuid() };
        var differentUserId = Guid.NewGuid();

        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                claims: [new Claim(JwtRegisteredClaimNames.Sub, differentUserId.ToString())],
                authenticationType: "TestAuth"
            )
        );

        var requirement = new OwnerOrAdminRequirement();
        var context = new AuthorizationHandlerContext(
            requirements: [requirement],
            user: user,
            resource: basket
        );

        var handler = new BasketAuthorizationHandler();

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirement_SubClaimMissing_DoesNotSucceed()
    {
        // Arrange
        var basket = new CustomerBasket { Id = Guid.NewGuid() };

        var user = new ClaimsPrincipal(
            new ClaimsIdentity(claims: [], authenticationType: "TestAuth")
        );

        var requirement = new OwnerOrAdminRequirement();
        var context = new AuthorizationHandlerContext(
            requirements: [requirement],
            user: user,
            resource: basket
        );

        var handler = new BasketAuthorizationHandler();

        // Act
        await handler.HandleAsync(context);

        // Assert
        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleRequirement_SubClaimNotAGuid_ThrowsFormatException()
    {
        // Arrange
        var basket = new CustomerBasket { Id = Guid.NewGuid() };

        var user = new ClaimsPrincipal(
            new ClaimsIdentity(
                claims: [new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid")],
                authenticationType: "TestAuth"
            )
        );

        var requirement = new OwnerOrAdminRequirement();
        var context = new AuthorizationHandlerContext(
            requirements: [requirement],
            user: user,
            resource: basket
        );

        var handler = new BasketAuthorizationHandler();

        // Act + Assert
        await Assert.ThrowsAsync<FormatException>(() => handler.HandleAsync(context));
    }

    
}
