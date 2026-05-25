using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using GoGameShop.Api.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace GoGameShop.Api.Features.Baskets.Authorization;

// Authorization handler is a class responsible for the evaluation
// of a requirement's properties
public class BasketAuthorizationHandler
    : AuthorizationHandler<OwnerOrAdminRequirement, CustomerBasket>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OwnerOrAdminRequirement requirement,
        CustomerBasket resource
    )
    {
        var currentUserId = context.User.FindFirstValue(GoGameShopClaimTypes.UserId);
        if (String.IsNullOrEmpty(currentUserId))
        {
            return Task.CompletedTask;
        }

        if (Guid.Parse(currentUserId) == resource.Id || context.User.IsInRole(Roles.Admin) == true)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// A resource-based handler is an authorization handler that specifies
// both a requirement and a resource type

// Authorization requirement is a collection of data parameters
// that a policy can use to evaluate the current user principle
public class OwnerOrAdminRequirement : IAuthorizationRequirement { }
