## Authentication & Authorization

### Authentication vs Authorization

**The problem:**
"Logged in" and "allowed to do this" sound like the same thing in casual conversation but they're not, and conflating them leads to muddled code — checking a role inside login logic, or skipping permission checks because the caller "is signed in." The framework needs to keep them distinct so each can be reasoned about independently.

**What they are:**
Two separate concepts:

- **Authentication** — *Who are you?* Validates the caller's identity by checking a token or credential.
- **Authorization** — *Are you allowed?* Checks whether the identified caller has permission to do what they're asking.

In ASP.NET Core, these are two separate middleware registrations and two separate service registrations. Authentication must run first — authorization depends on it knowing who the caller is.

---
### JWT Bearer Authentication

**The problem:**
Stateful sessions (a session ID stored in a server-side store) don't scale well across multiple servers or stateless deployments — every request needs a database lookup, and signing the user out everywhere is hard. Some scheme is needed where the server can identify the caller without keeping per-user state.

**What it does:**
JWT (JSON Web Token) is a compact, self-contained token format. A client receives a JWT from an identity provider after logging in and attaches it to subsequent requests via the `Authorization` header:

```
Authorization: Bearer eyJhbGci...
```

The server validates the token's signature and reads the claims embedded inside it — no database lookup needed. Identity travels with the request.

**In code — `Program.cs`:**
```csharp
builder
    .Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = "role";
    });
```

**Breaking it down:**

`AddAuthentication()` — registers the authentication services. On its own it does nothing — you must chain at least one scheme.

`AddJwtBearer(...)` — registers JWT Bearer as the default authentication scheme. ASP.NET Core will now look for a `Bearer` token on every request and validate it.

`MapInboundClaims = false` — by default, the JWT middleware remaps standard JWT claim names to long Microsoft-style names (`sub` → `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`). Setting this to `false` keeps claim names as they appear in the token (`sub`, `scope`, `role`, etc.), which is almost always what you want.

`RoleClaimType = "role"` — tells the framework which claim in the JWT represents the user's role. The default Microsoft claim name is a very long URL. Setting it to `"role"` means `.RequireRole("Admin")` and `User.IsInRole("Admin")` look for the `role` claim instead.

---
### Fallback Policy — Secure by Default

**The problem:**
Without a fallback, every new endpoint is public unless someone remembers to add `RequireAuthorization`. Forgetting that one call silently exposes data — and the failure mode is invisible in code review because the *missing* call is what causes it.

**What it does:**
`AddFallbackPolicy` registers a policy that automatically applies to every endpoint that has no explicit authorization configured. It flips the default: instead of endpoints being public unless locked down, they are locked down unless explicitly opened up. Forgetting an auth call now means the fallback kicks in — a safer failure mode.

**In code — `AuthorizationExtensions.cs`:**
```csharp
builder
    .Services.AddAuthorizationBuilder()
    .AddFallbackPolicy(
        Policies.UserAccess,
        authBuilder =>
        {
            authBuilder.RequireClaim(ClaimTypes.Scope, ApiAccessScope);
        }
    )
    .AddPolicy(
        Policies.AdminAccess,
        authBuilder =>
        {
            authBuilder.RequireClaim(ClaimTypes.Scope, ApiAccessScope);
            authBuilder.RequireRole(Roles.Admin);
        }
    );
```

The basket endpoints have no auth call on them — they are automatically protected by the `UserAccess` fallback.

`AddPolicy` — registers a named policy applied only when explicitly referenced via `RequireAuthorization(policyName)`. `AdminAccess` is still a named policy because it applies to a specific subset of endpoints.

---
### AllowAnonymous — Explicit Opt-Out

**The problem:**
A fallback policy locks down everything, but some endpoints genuinely need to be public — a game catalog the homepage reads before login, a health check, a static asset. Without an opt-out, the fallback would block those too.

**What it does:**
`.AllowAnonymous()` exempts an endpoint from all authorization checks, including the fallback policy. It makes the public intent explicit at the call site, so there's no ambiguity about whether an endpoint is open by accident or by design.

**In code:**
```csharp
app.MapGet("/games", ...).AllowAnonymous();
app.MapGet("/genres", ...).AllowAnonymous();
```

Without `.AllowAnonymous()`, the fallback would block unauthenticated clients from reading the game catalog — which would make the shop unusable. The call makes the intent explicit: this endpoint is intentionally public.

**The three-tier pattern in this project:**

| Endpoint | Auth call | Effective policy |
|----------|-----------|-----------------|
| `GET /games`, `GET /games/{id}`, `GET /genres`, `GET /ratings` | `.AllowAnonymous()` | Public |
| `GET /baskets/{userId}`, `PUT /baskets/{userId}` | *(none)* | `UserAccess` via fallback |
| `POST /games`, `PUT /games/{id}`, `DELETE /games/{id}` | `.RequireAuthorization(Policies.AdminAccess)` | `AdminAccess` |

---
### UseAuthorization Middleware Order

**The problem:**
Middleware runs in registration order, and a fallback policy is an aggressive default. If authorization runs before static-file serving, every image and asset request goes through the auth check first — and anonymous users get `401` for files that were meant to be public.

**What it does:**
Calling `app.UseAuthorization()` explicitly (instead of relying on the framework's auto-injection) lets you place it *after* `UseStaticFiles()`. Static-file requests then short-circuit before authorization runs, so assets load for everyone regardless of auth state.

**In code — `Program.cs`:**
```csharp
app.UseStaticFiles();       // serve wwwroot/ files first, before auth runs
app.UseAuthorization();     // then enforce auth on API endpoints

app.MapGames();
app.MapGetGenres();
// ...
```

---
### Roles and Policies — Static Constant Classes

**The problem:**
Policy and role names get referenced in at least two places — where they're registered, and on every endpoint that applies them. Repeating the bare string `"AdminAccess"` in many files means a typo doesn't fail to compile; it fails silently at runtime when an endpoint quietly stops being protected.

**What it does:**
`Roles` and `Policies` are static classes that hold the names as `const string` fields. Every reference goes through the constant, so a rename propagates everywhere and the compiler catches any stale string. `nameof(...)` keeps the constant name and its value in sync automatically.

**In code:**
```csharp
public static class Policies
{
    public const string UserAccess = nameof(UserAccess);
    public const string AdminAccess = nameof(AdminAccess);
}

public static class Roles
{
    public const string Admin = nameof(Admin);
}
```

`nameof(UserAccess)` returns the string `"UserAccess"` at compile time. If you rename the constant, `nameof` follows the rename automatically — the string stays in sync.

`static class` — the class cannot be instantiated. It's a pure namespace for constants, the same pattern as C's header-file defines.

---
### Resource-Based Authorization

**The problem:**
Some authorization rules can't be answered from the token alone. "Can this user edit *this specific basket*?" depends on who owns that basket — information that lives in the database, not the JWT. Policy-based auth can only inspect the token, so it can't express owner-based rules.

**What it does:**
Resource-based authorization loads the specific object being acted on and passes it to a handler so the decision can be made against that data. The built-in `AuthorizationHandler<TRequirement>` takes one generic. Adding a second one — `AuthorizationHandler<TRequirement, TResource>` — tells the framework this handler also expects a concrete resource object at runtime:

```csharp
public class BasketAuthorizationHandler
    : AuthorizationHandler<OwnerOrAdminRequirement, CustomerBasket>
```

This handler is only invoked when `CustomerBasket` is explicitly passed as the resource in an `AuthorizeAsync` call — it is not triggered by the fallback policy.

---
### `IAuthorizationRequirement` — The Rule Marker

A requirement is a class that represents "the rule being checked." It can be empty (as here) or carry data (e.g., `MinimumAgeRequirement(18)`). The handler reads the requirement and decides whether the current user satisfies it.

```csharp
public class OwnerOrAdminRequirement : IAuthorizationRequirement { }
```

The class body is empty because the rule has no parameters — either you own the basket or you're an admin. The type itself is the signal.

---
### `HandleRequirementAsync` — Succeed or Stay Silent

The method receives three things: the user context, the requirement, and the resource. Calling `context.Succeed(requirement)` grants access. Returning without calling it means the requirement was not satisfied — the framework treats silence as failure and returns `403 Forbidden`.

```csharp
var currentUserId = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
if (String.IsNullOrEmpty(currentUserId))
{
    return Task.CompletedTask; // silent failure → 403
}
if (Guid.Parse(currentUserId) == resource.Id || context.User.IsInRole(Roles.Admin))
{
    context.Succeed(requirement); // explicit grant
}
return Task.CompletedTask;
```

`FindFirstValue(JwtRegisteredClaimNames.Sub)` searches the token claims and returns the value of the `sub` claim — the user's unique ID — or `null` if the claim is absent.

The basket's `Id` equals the user's `sub` by design: when a basket is first created, it is assigned `Id = userId` where `userId` comes from the route. Since the route is the user's own ID (sourced from their token), the basket ID and the user ID are the same GUID.

---
### `IAuthorizationService` — Imperative Authorization in an Endpoint

Policy-based auth is **declarative** — you attach it to the endpoint at registration time. Resource-based auth is **imperative** — you call it manually inside the handler after loading the resource from the database.

`IAuthorizationService` is injected like any other service. In Minimal APIs, `ClaimsPrincipal` can be injected directly as a parameter — the framework resolves it from `HttpContext.User`.

```csharp
async (
    Guid userId,
    UpsertBasketDto upsertBasketDto,
    GoGameShopContext dbContext,
    IAuthorizationService authorizationService,
    ClaimsPrincipal user
) =>
{
    // load or create basket first...

    var authResult = await authorizationService.AuthorizeAsync(
        user,
        basket,
        new OwnerOrAdminRequirement()
    );

    if (!authResult.Succeeded)
    {
        return Results.Forbid();
    }

    await dbContext.SaveChangesAsync();
    return Results.NoContent();
}
```

The basket is loaded **before** the authorization check because the handler needs the actual object to compare the owner ID. Saving to the database happens **after** — only if the check passes.

`Results.Forbid()` returns `403 Forbidden` when the user is authenticated but not permitted. This is different from `Results.Unauthorized()` (`401`), which signals that the caller is not authenticated at all.

---
### Registering the Handler

Authorization handlers are registered with the DI container as `IAuthorizationHandler`. The framework discovers all registered handlers automatically when `AuthorizeAsync` is called.

```csharp
builder.Services.AddSingleton<IAuthorizationHandler, BasketAuthorizationHandler>();
```

`Singleton` is appropriate here because the handler holds no request-specific state — it reads from the context and resource passed in by the framework each time.

---
### Named Authentication Schemes

**The problem:**
`AddJwtBearer()` with no arguments registers a single default scheme. That works when there's only one identity provider. But once you integrate a real provider like Keycloak, you need to configure its specific settings (issuer URL, audience, etc.) separately from the generic defaults — and you need a way to tell the framework which scheme to use as the default.

**What it does:**
A named scheme lets you register multiple JWT bearer configurations under different names. You then set one as the default so the framework knows which to use when no scheme is explicitly specified.

**In code — `Program.cs`:**
```csharp
builder.Services.AddSingleton<KeycloakClaimsTransformer>();

builder
    .Services.AddAuthentication(Schemes.Keycloak)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
    })
    .AddJwtBearer(
        Schemes.Keycloak,
        options =>
        {
            options.Authority = "http://localhost:8080/realms/gogameshop";
            options.Audience = "gogameshop-api";
            options.MapInboundClaims = false;
            options.TokenValidationParameters.RoleClaimType = ClaimTypes.Role;
            options.RequireHttpsMetadata = false;
            options.Events = new JwtBearerEvents()
            {
                OnTokenValidated = context =>
                {
                    var transformer = context.HttpContext.RequestServices
                        .GetRequiredService<KeycloakClaimsTransformer>();

                    transformer.Transform(context);

                    return Task.CompletedTask;
                }
            };
        }
    );
```

**Breaking it down:**

`AddAuthentication(Schemes.Keycloak)` — sets the default authentication scheme to `"Keycloak"`. Every request will use this scheme unless an endpoint explicitly specifies another one.

`AddJwtBearer(options => ...)` — registers the unnamed/default JWT Bearer scheme. This one has no Authority or Audience — it's a generic fallback.

`AddJwtBearer(Schemes.Keycloak, options => ...)` — registers a second JWT Bearer scheme named `"Keycloak"` with provider-specific settings.

`Authority` — the URL of the identity provider's realm. The middleware fetches the OpenID Connect discovery document from `{Authority}/.well-known/openid-configuration` to learn the signing keys, issuer, and supported endpoints — no manual key configuration needed.

`Audience` — the expected `aud` claim in the token. The middleware rejects tokens that aren't intended for this API. In Keycloak, this matches the client ID.

`RequireHttpsMetadata = false` — allows fetching the discovery document over HTTP instead of HTTPS. Required for local development with Keycloak on `http://localhost:8080`. Never disable this in production.

The scheme name is a `const string` in a static class, following the same pattern as `Roles` and `Policies`:

```csharp
public static class Schemes
{
    public const string Keycloak = nameof(Keycloak);
}
```

---
### Custom ClaimTypes Constant

**The problem:**
The JWT `role` and `scope` claim names are referenced in multiple places — `RoleClaimType` configuration, `RequireClaim(...)` calls in policies, the claims transformer, etc. Hardcoding `"role"` and `"scope"` everywhere is the same magic-string problem that `Roles` and `Policies` classes solved. Additionally, `System.Security.Claims.ClaimTypes` already exists in .NET and uses long URL-style claim names that don't match the short names in JWTs from external providers.

**What it does:**
A project-specific `ClaimTypes` class with `const string` fields for the short JWT claim names used by the identity provider:

```csharp
namespace GoGameShop.Api.Shared.Authorization;

public static class ClaimTypes
{
    public const string Role = "role";

    public const string Scope = "scope";
}
```

This shadows the framework's `System.Security.Claims.ClaimTypes` — which is intentional. The project uses short claim names (`role`, `sub`, `scope`) from the JWT spec, not the long Microsoft-style URLs.

---
### Claims Transformation — Splitting the `scope` Claim

**The problem:**
The OAuth 2.0 spec defines the `scope` claim as a **single space-separated string** — `"openid profile gogameshop_api.all"`. ASP.NET Core's `RequireClaim("scope", "gogameshop_api.all")` does an exact-match comparison against a claim's value, so it sees the whole string and never matches a single scope inside it. Authorization built on individual scopes breaks unless something splits the string into one claim per scope first.

**What it does:**
`KeycloakClaimsTransformer` is a small project-owned class with a `Transform(TokenValidatedContext)` method. It is invoked from `JwtBearerEvents.OnTokenValidated` once per token validation — it pulls the single `scope` claim off the principal, splits it on spaces, and re-adds one `scope` claim per individual scope. After it runs, `RequireClaim(ClaimTypes.Scope, "gogameshop_api.all")` matches as expected.

**In code — `Shared/Authorization/KeycloakClaimsTransformer.cs`:**
```csharp
public class KeycloakClaimsTransformer(ILogger<KeycloakClaimsTransformer> logger)
{
    public void Transform(TokenValidatedContext context)
    {
        var identity = context.Principal?.Identity as ClaimsIdentity;

        var scopeClaim = identity?.FindFirst(ClaimTypes.Scope);
        if (scopeClaim is null)
        {
            return;
        }

        var scopes = scopeClaim.Value.Split(' ');
        identity?.RemoveClaim(scopeClaim);
        identity?.AddClaims(scopes.Select(s => new Claim(ClaimTypes.Scope, s)));

        foreach (var claim in context.Principal?.Claims ?? [])
        {
            logger.LogTrace("Claim: {ClaimType}, Value: {ClaimValue}",
                claim.Type, claim.Value);
        }
    }
}
```

The class is registered as a singleton and resolved from the request's service provider inside `OnTokenValidated`, because event handlers don't get constructor injection. Claim logging is at `Trace` level so it stays off in normal runs and can be enabled via `appsettings.json` only when actively debugging an authorization failure.

---
### Why a Custom Class Instead of `IClaimsTransformer`

**The problem:**
ASP.NET Core ships an `IClaimsTransformer` interface for exactly this kind of post-authentication claim shaping. The natural instinct is to use it. But its lifecycle is wrong for splitting the `scope` claim, and using it the wrong way leads to either duplicated claims or wasted CPU on every request.

**What `IClaimsTransformer` actually does:**
`IClaimsTransformer.TransformAsync` runs **on every authenticated request**, not once when the token is validated. The framework also documents that it may run multiple times for the same principal, so any implementation must be **idempotent** — running it twice must produce the same result as running it once. Splitting `"openid profile gogameshop_api.all"` once gives you three `scope` claims; running the same splitter again on a principal that now has three `scope` claims would either duplicate them or require an extra "have I already split?" check on every request.

**Why the custom class wins here:**
- **Runs once, not per request.** `OnTokenValidated` fires when the JWT middleware first builds the `ClaimsPrincipal` from the token. After that, the same principal is reused for the lifetime of the authentication — no further work needed on subsequent requests.
- **No idempotency burden.** Because it runs exactly once per token, the splitter doesn't have to detect and skip its own previous output.
- **Scoped to the Keycloak scheme.** `Events` is set on the named `Schemes.Keycloak` JWT bearer registration, so the transformation only applies to tokens from that scheme. An `IClaimsTransformer` is global — it runs against every authenticated principal regardless of scheme.
- **Plain class, plain DI.** It's just a class with a `Transform` method. No interface contract, no `ClaimsPrincipal` cloning that `IClaimsTransformer` implementations are expected to do, no ceremony.

The naming is deliberate — `KeycloakClaimsTransformer` describes the *purpose*, not the framework interface it implements. It deliberately does not implement `IClaimsTransformer`.

---
### Applying Policies to Endpoints

**The problem:**
A registered policy is just a definition — it doesn't enforce anything until something attaches it to a route. The fallback covers the default case, but anything stricter (like admin-only endpoints) needs an explicit hookup at the call site.

**What it does:**
`RequireAuthorization(policyName)` chains onto a Minimal API endpoint registration and tells the framework to enforce a named policy before the handler runs. If the request fails the policy, the framework short-circuits with `401 Unauthorized` (not authenticated) or `403 Forbidden` (authenticated but not authorized).

**In code:**
```csharp
// Any logged-in user with a valid API token
app.MapGet("/baskets/{userId}", ...).RequireAuthorization(Policies.UserAccess);
app.MapPut("/baskets/{userId}", ...).RequireAuthorization(Policies.UserAccess);

// Admin only
app.MapPost("/games", ...).RequireAuthorization(Policies.AdminAccess);
app.MapPut("/games/{id}", ...).RequireAuthorization(Policies.AdminAccess);
app.MapDelete("/games/{id}", ...).RequireAuthorization(Policies.AdminAccess);
```

**Endpoint authorization summary:**

| Method | Route | How | Effective policy |
|--------|-------|-----|-----------------|
| `GET` | `/games` | `.AllowAnonymous()` | Public |
| `GET` | `/games/{id}` | `.AllowAnonymous()` | Public |
| `GET` | `/genres` | `.AllowAnonymous()` | Public |
| `GET` | `/ratings` | `.AllowAnonymous()` | Public |
| `GET` | `/baskets/{userId}` | *(none — fallback)* | UserAccess |
| `PUT` | `/baskets/{userId}` | *(none — fallback)* | UserAccess |
| `POST` | `/games` | `.RequireAuthorization(Policies.AdminAccess)` | AdminAccess |
| `PUT` | `/games/{id}` | `.RequireAuthorization(Policies.AdminAccess)` | AdminAccess |
| `DELETE` | `/games/{id}` | `.RequireAuthorization(Policies.AdminAccess)` | AdminAccess |

---
