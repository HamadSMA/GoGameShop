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

---

### Typed HTTP Clients — `GamesClient`, `LookupClient`, `ServerBasketClient`

**The problem:**
The frontend needs to call multiple API endpoints — games, genres, ratings, baskets. Injecting a raw `HttpClient` everywhere and repeating URL construction, JSON deserialization, and error handling in each component is noisy and error-prone.

**What it does:**
A **typed HTTP client** is a plain class that takes `HttpClient` as a constructor parameter. `IHttpClientFactory` creates and manages the underlying `HttpClient` instance; the typed class just uses it. Each client in `Clients/` owns one API resource:

```csharp
public class GamesClient(HttpClient http)
{
    public async Task<GamesPageDto> GetGamesAsync(int page = 1, int pageSize = 5, string? name = null)
    {
        var url = $"games?pageNumber={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(name))
            url += $"&name={Uri.EscapeDataString(name)}";
        return await http.GetFromJsonAsync<GamesPageDto>(url) ?? new(0, []);
    }
    // CreateGameAsync, UpdateGameAsync, DeleteGameAsync...
}
```

`GamesClient` also handles the multipart form-data requirement for create/update — it builds `MultipartFormDataContent` from a `GameFormModel` (including the optional image file stream) so components never have to construct it directly.

**Registration in `Program.cs`:**
```csharp
builder.Services.AddHttpClient<GamesClient>(c => c.BaseAddress = new Uri(apiBase))
    .AddHttpMessageHandler<ApiAuthorizationHandler>();
```

`AddHttpClient<TClient>()` registers the typed client and its `HttpClient` with `IHttpClientFactory`. The `BaseAddress` is set once here — all relative URLs inside the client resolve against it. `.AddHttpMessageHandler<ApiAuthorizationHandler>()` chains the Bearer token injector onto every call made through that client. The same pattern is repeated for `LookupClient` and `ServerBasketClient`.

**`ApiAuthorizationHandler` must be `Transient`:**
```csharp
builder.Services.AddTransient<ApiAuthorizationHandler>();
```
`IHttpClientFactory` manages handler lifetimes independently — it requires handlers to be transient so it can control how they are reused and pooled. Registering as `Scoped` or `Singleton` causes a runtime error.

---

### Frontend Models — Records vs Mutable Class

**The problem:**
The frontend needs two kinds of types: shapes that represent API responses (read from JSON, never mutated), and a shape that Blazor's `EditForm` can bind to (needs settable properties).

**What it does:**
All API response types are **records** — immutable by default, with positional constructors that match the JSON field names:

```csharp
public record GameSummaryDto(Guid Id, string Name, string Genre, decimal Price, ...);
public record BasketItemDto(Guid Id, string Name, decimal Price, int Quantity, string ImageUri);
```

Records support `with` expressions, which create a modified copy without mutating the original — useful in `BasketState` for updating a quantity inline:

```csharp
items.Select(i => i.Id == gameId ? i with { Quantity = i.Quantity + 1 } : i)
```

`GameFormModel` is the exception — a **mutable class** with `{ get; set; }` properties. Blazor's `EditForm` / two-way `@bind` requires settable properties; records' init-only setters aren't enough for form binding.

---

### `BasketState` — Scoped In-Memory Cache

**The problem:**
Multiple components on the same page may need the basket: a navbar shows item count, a basket page shows the full list. Without coordination, each component would fire its own API call — wasted round-trips for the same data within a single render.

**What it does:**
`BasketState` is a **scoped service** (one instance per HTTP request). The first call to `GetBasketAsync()` fetches the basket from the API and caches it in `_cache`. Every subsequent call within the same request returns the cached value:

```csharp
public async Task<BasketDto?> GetBasketAsync()
{
    if (UserId == Guid.Empty) return null;
    _cache ??= await basketClient.GetBasketAsync(UserId);  // fetch once, cache for this request
    return _cache;
}
```

Any mutation (add, update, remove) calls `Sync()`, which writes the change to the API, clears `_cache` (so the next read fetches fresh data), and fires `OnChange`:

```csharp
private async Task Sync(IEnumerable<UpsertBasketItemDto> items)
{
    await basketClient.UpsertBasketAsync(UserId, items);
    _cache = null;
    OnChange?.Invoke();
}
```

`OnChange` is an `event Action?` — components subscribe to it in `OnInitialized` and call `StateHasChanged()` in the handler so their UI updates (e.g., a basket count badge in the navbar refreshes immediately after an add).

**User identity:**
```csharp
private Guid UserId =>
    Guid.TryParse(
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
        out var id) ? id : Guid.Empty;
```

`ClaimTypes.NameIdentifier` maps to Keycloak's `sub` claim — the user's unique ID in the realm. Because `MapInboundClaims = false` is set in the OIDC options, the claim name stays as the short string `"sub"` rather than a long Microsoft URL. `ClaimTypes.NameIdentifier` resolves to `"sub"` in that context, matching correctly.

---

### `Program.cs` — The Full Auth Stack

**The problem:**
Wiring cookie + OIDC authentication in one `Program.cs` involves several options that interact with each other. Getting one wrong (e.g., forgetting `SaveTokens`, wrong `ResponseType`, wrong `SignInScheme`) silently breaks different parts of the flow.

**What it does — annotated:**
```csharp
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme) // cookie is the default scheme
    .AddCookie(options =>
    {
        options.LoginPath = "/login"; // unauthenticated requests redirect here
        options.Events.OnValidatePrincipal = async context =>
        {
            // fires on every authenticated request; used to proactively refresh the token
            var refresher = context.HttpContext.RequestServices.GetRequiredService<CookieOidcRefresher>();
            await refresher.ValidateOrRefreshCookieAsync(context, OpenIdConnectDefaults.AuthenticationScheme);
        };
    })
    .AddOpenIdConnect(options =>
    {
        options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme; // after OIDC login, store result in the cookie
        options.MetadataAddress = kc["MetadataAddress"]!; // OIDC discovery document
        options.ClientId = kc["ClientId"];
        options.ClientSecret = kc["ClientSecret"];
        options.ResponseType = OpenIdConnectResponseType.Code; // authorization code flow
        options.RequireHttpsMetadata = false; // local dev only — Keycloak runs on HTTP
        options.SaveTokens = true; // persist access + refresh tokens into the cookie properties
        options.MapInboundClaims = false; // keep claim names as-is ("sub", "role", not Microsoft URLs)
        options.Scope.Clear(); // clear defaults (which include unwanted scopes)
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Scope.Add("gogameshop_api.all"); // the custom API scope
        options.TokenValidationParameters.RoleClaimType = "role"; // matches Keycloak's remapped claim
    });
```

**`/login` and `/logout` endpoints:**
```csharp
app.MapGet("/login", () =>
    Results.Challenge(
        new AuthenticationProperties { RedirectUri = "/" },
        [OpenIdConnectDefaults.AuthenticationScheme]
    ));
```
`Results.Challenge()` with the OIDC scheme triggers the redirect to Keycloak's login page. `RedirectUri` is where Keycloak sends the user after a successful login.

```csharp
app.MapPost("/logout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme,
        new AuthenticationProperties { RedirectUri = "/" });
}).RequireAuthorization();
```
Two sign-outs are required: the first clears the local session cookie; the second hits Keycloak's end-session endpoint, invalidating the Keycloak session so the user can't silently re-authenticate without entering credentials again. Order matters — cookie first, then OIDC.

---

### `CascadingAuthenticationState` and `AuthorizeRouteView`

**The problem:**
Components like `<AuthorizeView>` and pages that declare `[Authorize]` need access to the current authentication state. But individual components have no way to look up the state on their own — they'd need a service injection in every file, and the routing layer has no built-in hook to enforce auth attributes before a page renders.

**What it does:**
`CascadingAuthenticationState` is a wrapper component placed at the root of the tree (in `App.razor`) that broadcasts a `Task<AuthenticationState>` downward as a cascading value. Any descendant can receive it with `[CascadingParameter] private Task<AuthenticationState> AuthState { get; set; }` — no injection needed.

`AuthorizeRouteView` replaces `RouteView` in `Routes.razor`. Before rendering a page, it checks for `[Authorize]` or `[Authorize(Roles = "...")]` attributes. If the user doesn't meet the requirement, it redirects to the configured login path instead of rendering the page.

```razor
@* App.razor — broadcast auth state to the entire tree *@
<CascadingAuthenticationState>
    <Routes />
</CascadingAuthenticationState>
```

```razor
@* Routes.razor — enforce [Authorize] attributes at the routing layer *@
<AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" />
```

Without `CascadingAuthenticationState`, `AuthorizeRouteView` and `<AuthorizeView>` both throw at runtime because they can't find the cascaded `Task<AuthenticationState>`. Without `AuthorizeRouteView`, `[Authorize]` attributes on pages are silently ignored and pages render for everyone.

---

### `<AuthorizeView>` — Conditional Rendering by Auth State

**The problem:**
Some UI belongs only to signed-in users (the game grid, the cart badge, the logout button). Some belongs only to guests (the login link, a "sign in to browse" CTA). Hard-coding this with null checks on a service would scatter auth logic into every component.

**What it does:**
`<AuthorizeView>` renders its `<Authorized>` child content when the user is authenticated and `<NotAuthorized>` when they're not. An optional `Roles` attribute narrows it further — only users in that role see the `<Authorized>` block.

```razor
@* Home.razor — game grid for signed-in users, CTA for guests *@
<AuthorizeView>
    <Authorized>
        <div class="games-grid"> ... </div>
    </Authorized>
    <NotAuthorized>
        <a href="/login" class="btn-primary">Login / Register</a>
    </NotAuthorized>
</AuthorizeView>

@* NavMenu.razor — Catalog link only for admins *@
<AuthorizeView Roles="Admin">
    <li><a href="/catalog">Catalog</a></li>
</AuthorizeView>
```

`context` inside `<Authorized>` is a `AuthenticationState` — you can read claims from it: `context.User.FindFirst("email")?.Value`.

---

### `[Authorize]` — Page-Level Authorization

**The problem:**
`<AuthorizeView>` hides content, but it doesn't stop a user from navigating directly to a URL. A guest who knows the `/cart` URL would still see the page render.

**What it does:**
`[Authorize]` is an attribute placed on a page component. `AuthorizeRouteView` checks it before the page renders — unauthenticated users are redirected to the login path instead of reaching the component's `OnInitializedAsync` at all. `[Authorize(Roles = "Admin")]` restricts further to a specific role.

```razor
@page "/cart"
@attribute [Authorize]          @* any signed-in user *@

@page "/catalog"
@attribute [Authorize(Roles = "Admin")]   @* admin only *@
```

This is the difference from `<AuthorizeView>`: `<AuthorizeView>` controls what's rendered inside a page that already loaded; `[Authorize]` controls whether the page loads at all.

---

### `[SupplyParameterFromQuery]` — Query String Parameters

**The problem:**
Pagination (`?page=2`) and search (`?name=zelda`) live in the URL query string. Parsing `HttpContext.Request.Query` manually in every component that needs them is repetitive and type-unsafe.

**What it does:**
`[SupplyParameterFromQuery]` maps a query string key to a component property automatically. Blazor reads the value from the URL, converts it to the declared type, and sets the property before `OnInitializedAsync` runs. The `Name` argument overrides the key name when it differs from the property name.

```csharp
[SupplyParameterFromQuery(Name = "page")] public int page { get; set; } = 1;
[SupplyParameterFromQuery] public string? name { get; set; }
```

For `?page=3&name=zelda`, Blazor sets `page = 3` and `name = "zelda"` automatically. The `= 1` default applies when the key is absent from the URL.

---

### Form Handling in Static SSR — `@formname`, `[SupplyParameterFromForm]`, `AntiforgeryToken`

**The problem:**
Static SSR has no persistent JavaScript — all user actions go through standard HTML form POST. When a page has multiple forms (a cart page with "update quantity" and "remove item" buttons), there needs to be a way to tell the forms apart on the server and bind their posted fields to separate C# models.

**What it does:**
`@formname="foo"` gives a Razor form a name. On POST, Blazor reads a hidden field that carries this name and routes the posted values to the matching `[SupplyParameterFromForm(FormName = "foo")]` parameter. Properties on the bound model are populated from the form fields by name.

`<AntiforgeryToken />` renders a hidden CSRF token field that `app.UseAntiforgery()` validates on every POST — without it, the middleware rejects the request.

```razor
<form method="post" @formname="update-qty">
    <AntiforgeryToken />
    <input type="hidden" name="QtyModel.GameId" value="@item.Id" />
    <select name="QtyModel.Quantity" onchange="this.form.submit()"> ... </select>
</form>

<form method="post" @formname="remove-item">
    <AntiforgeryToken />
    <input type="hidden" name="RemoveModel.GameId" value="@item.Id" />
    <button type="submit">Remove</button>
</form>
```

```csharp
[SupplyParameterFromForm(FormName = "update-qty")]  public QtyForm?    QtyModel    { get; set; }
[SupplyParameterFromForm(FormName = "remove-item")] public RemoveForm? RemoveModel { get; set; }
```

Only one of the two will be non-null per POST — whichever form was submitted. `OnInitializedAsync` checks which one has a value and handles it:

```csharp
protected override async Task OnInitializedAsync()
{
    if (QtyModel?.GameId is Guid qtyId)   { /* update */ return; }
    if (RemoveModel?.GameId is Guid removeId) { /* remove */ return; }
    basket = await Basket.GetBasketAsync(); // normal GET load
}
```

The `Nav.NavigateTo("/cart", forceLoad: true)` after a mutation forces a full page reload — this clears the POST state so a browser refresh doesn't resubmit the form.

---

### `[StreamRendering]` — Incremental HTML Delivery

**The problem:**
A page that awaits an API call makes the browser wait for the entire response before showing anything — the user sees a blank screen for however long the API takes.

**What it does:**
`[StreamRendering]` on a page component tells Blazor to flush the initial HTML to the browser immediately, including whatever the component renders before its first `await`. The component then streams updated HTML as async operations complete.

```razor
@page "/catalog"
@attribute [StreamRendering]

@if (gamesPage is null)
{
    <p>Loading...</p>    @* sent to browser immediately *@
}
else
{
    <table> ... </table>  @* streamed in once the API call completes *@
}
```

The user sees "Loading..." almost instantly instead of staring at nothing. This works because HTTP supports chunked transfer encoding — the server sends pieces of the response body as they become available rather than buffering the whole thing.

---

### The `Pagination` Component — Shared Child Components

**The problem:**
Both `Home.razor` and `Catalog.razor` need pagination links. Duplicating the HTML in each page is error-prone — a bug fix or style change would need to be applied in both places.

**What it does:**
A shared Razor component (no `@page` directive, lives in `Components/`) encapsulates the pagination UI. Parent pages use it like an HTML tag and pass data via `[Parameter]` properties. `[EditorRequired]` marks parameters that must always be provided — the compiler warns if a caller omits them.

```razor
@* Pagination.razor *@
@if (TotalPages > 1)
{
    <div class="pagination">
        @for (var i = 1; i <= TotalPages; i++)
        {
            var url = string.IsNullOrEmpty(Query)
                ? $"{BaseUrl}?page={i}"
                : $"{BaseUrl}?page={i}&{Query}";
            <a href="@url" class="@(i == CurrentPage ? "active" : "")">@i</a>
        }
    </div>
}

@code {
    [Parameter, EditorRequired] public int     CurrentPage { get; set; }
    [Parameter, EditorRequired] public int     TotalPages  { get; set; }
    [Parameter, EditorRequired] public string  BaseUrl     { get; set; } = default!;
    [Parameter]                 public string? Query       { get; set; }
}
```

The optional `Query` parameter lets callers append extra state to page links — Catalog passes `name=zelda` so search terms survive pagination clicks.

Usage:
```razor
<Pagination CurrentPage="page" TotalPages="gamesPage.TotalPages" BaseUrl="/catalog"
            Query="@(string.IsNullOrEmpty(name) ? null : $"name={Uri.EscapeDataString(name)}")" />
```

---

### Gravatar in `LoginDisplay`

**The problem:**
The nav needs to show who's logged in. Storing or serving user avatars is a whole feature on its own.

**What it does:**
[Gravatar](https://gravatar.com) is a global avatar service: you hash an email address with MD5 and embed the hash in an image URL — Gravatar serves whatever avatar the user registered for that email, or a fallback if they have none.

```csharp
private static string GravatarUrl(string? email)
{
    if (string.IsNullOrWhiteSpace(email)) return string.Empty;
    var hash = Convert.ToHexString(
        System.Security.Cryptography.MD5.HashData(
            System.Text.Encoding.UTF8.GetBytes(email.Trim().ToLower())))
        .ToLower();
    return $"https://www.gravatar.com/avatar/{hash}?d=identicon";
}
```

- `email.Trim().ToLower()` — Gravatar requires the email to be lowercase with no surrounding whitespace before hashing.
- `d=identicon` — the fallback when no Gravatar is registered: a deterministic geometric pattern generated from the hash, so every user gets a unique-looking avatar automatically.
- MD5 is fine here — this isn't a security use, just a lookup key. The Gravatar spec defines it.

The email comes from the `"email"` claim on `context.User`, which Keycloak includes because `email` scope was requested in the OIDC options.

---

### `returnUrl` — Redirect After Login

**The problem:**
Without a return URL, clicking "Login" on any page always drops the user on `/` after sign-in — they lose their place. A user browsing `/game/abc` who gets logged out mid-session has to navigate back manually after re-authenticating.

**What it does:**
`LoginDisplay` encodes the current page URL into the login link. The `/login` minimal API reads it back and passes it as `RedirectUri` in the `AuthenticationProperties` — Keycloak uses this as the post-login destination.

```razor
@* LoginDisplay.razor — encode the current URL into the login link *@
<a href="/login?returnUrl=@Uri.EscapeDataString(Nav.Uri)" class="btn-primary">Login</a>
```

```csharp
// Program.cs — /login endpoint reads returnUrl
app.MapGet("/login", (string? returnUrl) =>
    Results.Challenge(
        new AuthenticationProperties { RedirectUri = returnUrl ?? "/" },
        [OpenIdConnectDefaults.AuthenticationScheme]
    ));
```

`Uri.EscapeDataString` percent-encodes the URL so it survives as a query string value — without it, a URL like `/game/abc?page=2` would break the outer query string parsing. `returnUrl ?? "/"` falls back to the home page when the parameter is absent (e.g., clicking login from the nav directly).
