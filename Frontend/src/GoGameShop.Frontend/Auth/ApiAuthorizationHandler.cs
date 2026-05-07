using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace GoGameShop.Frontend.Auth;

public class ApiAuthorizationHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken
    )
    {
        var ctx = httpContextAccessor.HttpContext;
        if (ctx is not null)
        {
            var token = await ctx.GetTokenAsync("access_token");
            if (!string.IsNullOrEmpty(token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        return await base.SendAsync(request, cancellationToken);
    }
}
