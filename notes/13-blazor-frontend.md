## Blazor Frontend

### What Is Blazor

**The problem:**
Building a UI for an ASP.NET Core API would traditionally mean context-switching to a completely separate JavaScript ecosystem — a React or Vue app with its own toolchain, packages, and language. For a .NET developer, that doubles the mental load before a single component is written.

**What it does:**
Blazor is the .NET framework for building interactive web UIs in C#. Instead of `.tsx`/`.vue` files, you write **Razor components** (`.razor` files) that mix HTML markup with C# logic in one file. The component model is similar in concept to React — composable, data-driven, event-driven — but the language is C# and the compilation is handled by the .NET SDK.

**How it fits the project:**
`GoGameShop.Frontend` is a Blazor app (net10, Static SSR) that will serve as the user-facing storefront — product listings, game detail pages, basket, and Keycloak-authenticated checkout. It calls the `GoGameShop.Api` backend over HTTP and lives at `http://localhost:5003` in development.

---

### Razor Components

**The problem:**
HTML is static markup; a Razor component needs to mix markup with C# logic, event handling, and data binding — all in one file without becoming unreadable.

**What it does:**
A `.razor` file has three optional sections, separated by blocks:

```razor
@page "/games"                     @* route directive — makes this page routable *@
@inject HttpClient Http            @* dependency injection *@

<h1>Games</h1>                     @* HTML markup *@
<p>Count: @games.Count</p>         @* C# expression rendered as text *@

@code {                            @* C# class members (fields, methods, lifecycle) *@
    private List<Game> games = [];

    protected override async Task OnInitializedAsync()
    {
        games = await Http.GetFromJsonAsync<List<Game>>("/games") ?? [];
    }
}
```

Key rules:
- `@page "/route"` makes a component a routable page (appears in `Pages/`)
- `@` introduces a C# expression inline; `@{ ... }` is a C# block
- Lifecycle methods (`OnInitializedAsync`, `OnParametersSetAsync`) are overrides on the base class `ComponentBase`
- Components that aren't pages have no `@page` directive and are used like HTML tags: `<GameCard Game="item" />`

**In the project:**
The scaffold has `App.razor` (the root HTML shell), `Routes.razor` (the router), `_Imports.razor` (global using directives for all components), and starter pages in `Components/Pages/`.

---

### Blazor Hosting Models

**The problem:**
"Blazor" is actually three different execution models, and choosing the wrong one affects performance, SEO, bundle size, and what C# APIs are available.

**What the options are:**

| Model | Where C# runs | How HTML reaches the browser | Notes |
|---|---|---|---|
| **Static SSR** | Server, per-request | Pre-rendered HTML, no live connection | Like traditional Razor Pages; best for SEO and simplicity |
| **Blazor Server** | Server, continuously | DOM diffs over a persistent SignalR WebSocket | Real-time UI; requires constant server connection |
| **Blazor WebAssembly** | Browser via .NET WASM | Downloads .NET runtime + app (~5–10 MB initial load) | Fully client-side; no server required after download |
| **Blazor Web App (auto)** | Starts as SSR, upgrades to WASM | SSR on first load, WASM takes over once downloaded | Best of both; most complex to configure |

**What Static SSR does:**
Each navigation request renders the component tree to HTML on the server and sends it to the browser — there's no live server connection and no WASM download. Forms work via standard HTTP POST with antiforgery tokens; for any interactivity (dropdowns, live search) individual components can opt into Server or WASM rendering with `@rendermode`.

**Why Static SSR here:**
It's the simplest model: one server, familiar request/response semantics, no WASM download, good SEO. The storefront's read-heavy pages (catalogue, game detail) don't need real-time interactivity — they render once per request, which Static SSR handles well.

**The `Program.cs` registration:**
```csharp
builder.Services.AddRazorComponents();   // registers Razor component infrastructure

app.UseAntiforgery();                    // required for form POST with antiforgery tokens
app.MapStaticAssets();                   // serves wwwroot/ with fingerprinted URLs
app.MapRazorComponents<App>();           // App.razor is the root; discovers all routable pages
```

`BlazorDisableThrowNavigationException` in the `.csproj` suppresses a navigation exception that fires during SSR when a component redirects — it would otherwise crash the render pipeline before the redirect can complete.

---

### Keycloak Integration — The `gogameshop-frontend` Client

**The problem:**
The frontend needs to log users in via Keycloak so that API calls can carry a valid JWT. But a web app hosted on a server is different from a JavaScript SPA or a mobile app — it can keep a secret, which changes which OAuth flow it uses.

**What it does:**
The `gogameshop-frontend` Keycloak client is a **confidential client**: it has a `clientSecret` and is not marked `publicClient`. This matters because ASP.NET Core's OpenID Connect (OIDC) middleware runs server-side and can securely store the secret — unlike a browser-hosted SPA, where the secret would be visible to anyone who opens DevTools.

The OIDC flow the frontend uses:
1. User visits a protected page → middleware redirects to Keycloak's login page
2. User enters credentials → Keycloak redirects back to `http://localhost:5003/signin-oidc` with an authorization code
3. The middleware exchanges the code for tokens using the client secret (server-to-server, never visible to the browser)
4. The session is established; subsequent requests carry a session cookie

The redirect URIs in the Keycloak client match the middleware's defaults:
- **Sign-in callback:** `http://localhost:5003/signin-oidc`
- **Sign-out callback:** `http://localhost:5003/signout-callback-oidc`

The `postman` client in the same realm allows Postman to authenticate against the same Keycloak realm for manual API testing (`https://oauth.pstmn.io/v1/browser-callback`).

**Configuration (`appsettings.json`):**
```json
{
  "Keycloak": {
    "MetadataAddress": "http://localhost:8080/realms/gogameshop/.well-known/openid-configuration",
    "ClientId": "gogameshop-frontend",
    "ClientSecret": "..."
  },
  "ApiBaseUrl": "http://localhost:5002"
}
```

`MetadataAddress` is the OIDC discovery document — the middleware fetches it at startup to find the authorization endpoint, token endpoint, and JWKS URI automatically. This is the same discovery document approach the API uses for JWT validation, just consumed by the OIDC middleware instead of the JWT bearer middleware.

---

### Token Storage in Static SSR — The Cookie Session

**The problem:**
In a browser-hosted SPA, the access token lives in `localStorage` or `sessionStorage` and gets attached to fetch calls by JavaScript. In Static SSR there's no persistent JavaScript — every request is a fresh HTTP round-trip. The token has to travel with the request some other way.

**What it does:**
ASP.NET Core's cookie authentication middleware stores the access token, refresh token, and expiry inside the **encrypted session cookie**. The cookie travels with every request automatically (the browser sends it), the middleware decrypts it and reconstructs the principal, and the token is available via `HttpContext.GetTokenAsync("access_token")`.

The cookie is encrypted with ASP.NET Core Data Protection — the browser only ever sees opaque bytes, never the raw token. This is why it's safe: the token never touches browser storage or JavaScript.

**The chain:**
1. OIDC middleware completes the authorization code exchange and receives the access + refresh tokens
2. Cookie middleware saves them into the encrypted cookie and sends it to the browser
3. On every subsequent request the browser sends the cookie; middleware decrypts it and restores `HttpContext.User` and the stored tokens
4. Components call `HttpContext.GetTokenAsync("access_token")` to retrieve the current access token

---

### `CookieOidcRefresher` — Proactive Token Refresh

**The problem:**
Access tokens are short-lived (typically 5–15 minutes). The session cookie lives much longer (days or weeks). Without intervention, the access token inside the cookie quietly expires while the session cookie is still valid — and the next API call with that stale token gets a `401`. The user has a valid session but all authenticated requests fail.

**What it does:**
`CookieOidcRefresher` is a service called from the cookie authentication's `OnValidatePrincipal` event. Every time a request arrives and the cookie middleware validates the cookie, this class checks whether the access token expires within the next 5 minutes. If it does, it proactively hits the token endpoint directly (`grant_type=refresh_token`) to get a fresh access token before the current request continues.

```csharp
// Within 5 minutes of expiry → refresh now
if (DateTimeOffset.UtcNow < expiresAt - TimeSpan.FromMinutes(5))
    return; // still valid, nothing to do

// Call the token endpoint directly (backchannel — server-to-server)
using var response = await opts.Backchannel.PostAsync(tokenEndpoint,
    new FormUrlEncodedContent(new Dictionary<string, string>
    {
        ["grant_type"]    = "refresh_token",
        ["client_id"]     = opts.ClientId!,
        ["client_secret"] = opts.ClientSecret!,
        ["refresh_token"] = context.Properties.GetTokenValue("refresh_token")!,
    }), cancellationToken);

if (!response.IsSuccessStatusCode)
{
    context.RejectPrincipal(); // refresh failed → force re-login
    return;
}

// Update the stored tokens and signal the cookie to be reissued
context.Properties.UpdateTokenValue("access_token", message.AccessToken!);
context.Properties.UpdateTokenValue("refresh_token", message.RefreshToken!);
context.Properties.UpdateTokenValue("expires_at", newExpiry.ToString("o"));
context.ShouldRenew = true; // tells the cookie middleware to reissue the cookie
```

The backchannel call (`opts.Backchannel`) uses the OIDC middleware's own `HttpClient` — it already has the token endpoint URL from the discovery document and the correct timeouts. `context.ShouldRenew = true` tells the cookie middleware to write an updated cookie in the response, so the fresh tokens replace the expiring ones.

If the refresh fails (e.g. the refresh token itself has expired), `RejectPrincipal()` marks the session invalid and the next request triggers a redirect to Keycloak's login page.

**Why this approach:**
It's pull-based — tokens are refreshed on the request that needs them, not on a background timer. That means no background thread, no race conditions, and no wasted refreshes during idle periods. The 5-minute window gives the current request enough time to complete with the existing token even if the refresh call takes a moment.

---

### `ApiAuthorizationHandler` — Attaching the Bearer Token to API Calls

**The problem:**
The frontend makes HTTP calls to the `GoGameShop.Api` backend to fetch game data, the basket, etc. The API's endpoints are protected — they require a `Bearer` token in the `Authorization` header. The question is: where does that token come from and how does it get onto every outgoing request without duplicating the lookup in every service class?

**What it does:**
`ApiAuthorizationHandler` is a **DelegatingHandler** — .NET's name for HttpClient middleware. A DelegatingHandler wraps an inner handler and runs code before and after every HTTP call made through a registered `HttpClient`. This one reads the access token from the current request's cookie session and sets the `Authorization` header:

```csharp
public class ApiAuthorizationHandler(IHttpContextAccessor httpContextAccessor)
    : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await httpContextAccessor.HttpContext?.GetTokenAsync("access_token");
        if (!string.IsNullOrEmpty(token))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return await base.SendAsync(request, cancellationToken); // pass through to the real call
    }
}
```

**`IHttpContextAccessor` — why it's needed:**
In Static SSR, components run on the server during the request, but `HttpClient` instances are singletons shared across requests — they don't inherently know which request's session to read from. `IHttpContextAccessor` provides access to the ambient `HttpContext` for the current request, bridging the gap between the singleton `HttpClient` and the per-request cookie session.

**How it fits into the chain:**
```
Component calls Http.GetFromJsonAsync(...)
  → ApiAuthorizationHandler.SendAsync()        // reads token from HttpContext, sets header
    → [inner handler — actual HTTP call]
      → GoGameShop.Api (receives Bearer token)
```

The handler is registered on a named or typed `HttpClient` in `Program.cs`. Once wired up, any service that injects that `HttpClient` gets authenticated calls for free — no per-service token lookup needed.
