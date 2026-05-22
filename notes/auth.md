## Authentication & Authorization

### Authentication vs Authorization

"Logged in" and "allowed to do this" sound like the same thing in casual conversation but they're not, and conflating them leads to muddled code: checking a role inside login logic, or skipping permission checks because the caller "is signed in." The framework keeps them distinct so each can be reasoned about independently.

- **Authentication** — *Who are you?* Validates the caller's identity by checking a token or credential.
- **Authorization** — *Are you allowed?* Checks whether the identified caller has permission to do what they're asking.

In ASP.NET Core, these are two separate middleware registrations and two separate service registrations. Authentication must run first, because authorization depends on it knowing who the caller is.

---
### Role-Based Authorization

Different users have different levels of access. An admin can create and delete games; a regular customer can only browse and manage their basket. Role-based authorization assigns one or more named roles to a user (for example, `Admin` or `Customer`) and checks for the required role before allowing access. The role comes from a claim inside the JWT. In this project, Keycloak sets a `"role"` claim on the token and the framework checks its value against the required role string.

```csharp
authBuilder.RequireRole(Roles.Admin);
```

`RequireRole` is syntactic sugar over `RequireClaim` that specifically targets the claim configured as `RoleClaimType`. If the `role` claim in the token does not contain `"Admin"`, the check fails and the framework returns `403 Forbidden`.

**Use cases:**

- **Admin-only write operations.** `POST /games`, `PUT /games/{id}`, and `DELETE /games/{id}` all require the `Admin` role. A logged-in customer gets `403`; an admin gets through.
- **Multi-tier access.** A future `Moderator` role could allow game edits but not deletions. Each role maps to a different policy.
- **Coarse-grained decisions.** When the answer is simply "is this user an admin or not?", role-based auth is the right fit. No resource loading or complex policy logic is needed.

---
### Claims-Based Authorization

Not every access decision maps cleanly to a role. A user might have permission to call the API at all (expressed as a scope claim) but not hold an admin role. Roles are a blunt instrument when permissions are more granular or expressed as arbitrary facts about the user: their tenant, subscription tier, or verified email status.

Claims-based authorization checks for the presence of a specific claim (and optionally a specific value) in the user's token. Any key-value pair embedded in a JWT is a claim. `RequireClaim` builds a requirement that a named claim must exist and, when a value is provided, must match.

```csharp
authBuilder.RequireClaim(ClaimTypes.Scope, ApiAccessScope);
```

This gates access on the caller having a specific OAuth scope. A request that carries a valid Keycloak token but was not granted `gogameshop_api.all` fails here regardless of any role.

**Use cases:**

- **API access gating.** Every endpoint (via the fallback policy) requires `scope = gogameshop_api.all`. A valid JWT that lacks this scope is still rejected.
- **Tenant isolation.** A `tenant_id` claim on the token could gate access to tenant-specific resources without a separate role.
- **Subscription tiers.** A `plan` claim with values like `free` or `premium` could unlock premium endpoints.
- **Verified identity.** An `email_verified = true` claim could be required before allowing profile changes.

---
### Policy-Based Authorization

Single-requirement checks (`RequireRole`, `RequireClaim`) don't compose well when access depends on multiple conditions. Combining them inline at every endpoint leads to repetition and no single place to read the access rules for the whole system.

A policy is a named, reusable bundle of one or more requirements. You define it once at startup and attach it to endpoints by name. The framework evaluates all requirements in the policy and only grants access when every one passes.

```csharp
.AddPolicy(
    Policies.AdminAccess,
    authBuilder =>
    {
        authBuilder.RequireClaim(ClaimTypes.Scope, ApiAccessScope);
        authBuilder.RequireRole(Roles.Admin);
    }
);
```

`AdminAccess` combines a scope check and a role check into a single named unit. Endpoints reference the name, not the individual requirements.

**Use cases:**

- **Composite rules.** `AdminAccess` requires both a valid API scope and the `Admin` role. Neither check alone is sufficient; both must pass.
- **Fallback / default gate.** `UserAccess` is registered as the fallback policy, automatically protecting every endpoint not otherwise configured.
- **Centralized rule definitions.** All access rules live in `AuthorizationExtensions.cs`. Changing a rule propagates to every endpoint that references the policy by name.
- **Explicit endpoint lockdown.** `RequireAuthorization(Policies.AdminAccess)` ties the composite rule to specific routes at registration time, making the intent visible at the call site.

---
### JWT Bearer Authentication

Stateful sessions (a session ID stored in a server-side store) don't scale well across multiple servers or stateless deployments: every request needs a database lookup, and signing the user out everywhere is hard. JWT (JSON Web Token) is the stateless alternative. A client receives a JWT from an identity provider after logging in and attaches it to subsequent requests via the `Authorization` header:

```
Authorization: Bearer eyJhbGci...
```

The server validates the token's signature and reads the claims embedded inside it, with no database lookup needed. Identity travels with the request.

**In code (`Program.cs`):**
```csharp
builder
    .Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = "role";
    });
```

`AddAuthentication()` registers the authentication services. On its own it does nothing; you must chain at least one scheme.

`AddJwtBearer(...)` registers JWT Bearer as the default authentication scheme. ASP.NET Core will now look for a `Bearer` token on every request and validate it.

`MapInboundClaims = false` keeps claim names as they appear in the token (`sub`, `scope`, `role`, etc.) rather than remapping them to long Microsoft-style URLs like `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`. This is almost always what you want when working with external identity providers.

`RoleClaimType = "role"` tells the framework which claim in the JWT represents the user's role. Setting it to `"role"` means `.RequireRole("Admin")` and `User.IsInRole("Admin")` look for the `role` claim instead of the default long-URL equivalent.

---
### Fallback Policy — Secure by Default

Without a fallback, every new endpoint is public unless someone remembers to add `RequireAuthorization`. Forgetting that one call silently exposes data, and the failure mode is invisible in code review because the *missing* call is what causes it.

`AddFallbackPolicy` registers a policy that automatically applies to every endpoint that has no explicit authorization configured. It flips the default: instead of endpoints being public unless locked down, they are locked down unless explicitly opened up. Forgetting an auth call now means the fallback kicks in, which is a safer failure mode.

**In code (`AuthorizationExtensions.cs`):**
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

The basket endpoints have no auth call on them; they are automatically protected by the `UserAccess` fallback.

`AddPolicy` registers a named policy applied only when explicitly referenced via `RequireAuthorization(policyName)`. `AdminAccess` is still a named policy because it applies to a specific subset of endpoints.

---
### AllowAnonymous — Explicit Opt-Out

A fallback policy locks down everything, but some endpoints genuinely need to be public: a game catalog the homepage reads before login, a health check, a static asset. `.AllowAnonymous()` exempts an endpoint from all authorization checks, including the fallback policy. It makes the public intent explicit at the call site, so there's no ambiguity about whether an endpoint is open by accident or by design.

**In code:**
```csharp
app.MapGet("/games", ...).AllowAnonymous();
app.MapGet("/genres", ...).AllowAnonymous();
```

Without `.AllowAnonymous()`, the fallback would block unauthenticated clients from reading the game catalog, which would make the shop unusable. The call makes the intent explicit: this endpoint is intentionally public.

**The three-tier pattern in this project:**

| Endpoint | Auth call | Effective policy |
|----------|-----------|-----------------|
| `GET /games`, `GET /games/{id}`, `GET /genres`, `GET /ratings` | `.AllowAnonymous()` | Public |
| `GET /baskets/{userId}`, `PUT /baskets/{userId}` | *(none)* | `UserAccess` via fallback |
| `POST /games`, `PUT /games/{id}`, `DELETE /games/{id}` | `.RequireAuthorization(Policies.AdminAccess)` | `AdminAccess` |

---
### UseAuthorization Middleware Order

Middleware runs in registration order, and a fallback policy is an aggressive default. If authorization runs before static-file serving, every image and asset request goes through the auth check first, and anonymous users get `401` for files that were meant to be public.

Calling `app.UseAuthorization()` explicitly lets you place it *after* `UseStaticFiles()`. Static-file requests then short-circuit before authorization runs, so assets load for everyone regardless of auth state.

**In code (`Program.cs`):**
```csharp
app.UseStaticFiles();       // serve wwwroot/ files first, before auth runs
app.UseAuthorization();     // then enforce auth on API endpoints

app.MapGames();
app.MapGetGenres();
// ...
```

---
### Roles and Policies — Static Constant Classes

Policy and role names get referenced in at least two places: where they're registered, and on every endpoint that applies them. Repeating the bare string `"AdminAccess"` in many files means a typo doesn't fail to compile; it fails silently at runtime when an endpoint quietly stops being protected.

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

`nameof(UserAccess)` returns the string `"UserAccess"` at compile time. If you rename the constant, `nameof` follows the rename automatically so the string stays in sync.

`static class` means the class cannot be instantiated. It's a pure namespace for constants, the same pattern as C's header-file defines.

---
### Resource-Based Authorization

Some authorization rules can't be answered from the token alone. "Can this user edit *this specific basket*?" depends on who owns that basket, which is information that lives in the database, not the JWT. Policy-based auth can only inspect the token, so it can't express owner-based rules.

Resource-based authorization loads the specific object being acted on and passes it to a handler so the decision can be made against that data. The built-in `AuthorizationHandler<TRequirement>` takes one generic. Adding a second one (`AuthorizationHandler<TRequirement, TResource>`) tells the framework this handler also expects a concrete resource object at runtime:

```csharp
public class BasketAuthorizationHandler
    : AuthorizationHandler<OwnerOrAdminRequirement, CustomerBasket>
```

This handler is only invoked when `CustomerBasket` is explicitly passed as the resource in an `AuthorizeAsync` call; it is not triggered by the fallback policy.

**Use cases:**

- **Owner-only basket access.** "Can this user read or modify this basket?" depends on who owns it. The handler loads the basket, compares `basket.Id` to the token's `sub` claim, and only succeeds when they match (or the user is an admin).
- **Author-only content edits.** A future review or comment system could restrict edits to the user who created the record, checked against the resource's `AuthorId` field.
- **Shared resource with explicit members.** An order accessible to the purchaser and the assigned fulfillment staff, where membership is stored on the order itself, not derivable from the token.
- **Soft-delete or status guards.** A resource handler can inspect a resource's status (for example, `Order.IsLocked`) and deny access even to the owner when the record is in a protected state.

---
### `IAuthorizationRequirement` — The Rule Marker

A requirement is a class that represents "the rule being checked." It can be empty (as here) or carry data (e.g., `MinimumAgeRequirement(18)`). The handler reads the requirement and decides whether the current user satisfies it.

```csharp
public class OwnerOrAdminRequirement : IAuthorizationRequirement { }
```

The class body is empty because the rule has no parameters: either you own the basket or you're an admin. The type itself is the signal.

---
### `HandleRequirementAsync` — Succeed or Stay Silent

The method receives three things: the user context, the requirement, and the resource. Calling `context.Succeed(requirement)` grants access. Returning without calling it means the requirement was not satisfied; the framework treats silence as failure and returns `403 Forbidden`.

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

`FindFirstValue(JwtRegisteredClaimNames.Sub)` searches the token claims and returns the value of the `sub` claim (the user's unique ID) or `null` if the claim is absent.

The basket's `Id` equals the user's `sub` by design: when a basket is first created, it is assigned `Id = userId` where `userId` comes from the route. Since the route is the user's own ID (sourced from their token), the basket ID and the user ID are the same GUID.

---
### `IAuthorizationService` — Imperative Authorization in an Endpoint

Policy-based auth is **declarative**: you attach it to the endpoint at registration time. Resource-based auth is **imperative**: you call it manually inside the handler after loading the resource from the database.

`IAuthorizationService` is injected like any other service. In Minimal APIs, `ClaimsPrincipal` can be injected directly as a parameter; the framework resolves it from `HttpContext.User`.

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

The basket is loaded **before** the authorization check because the handler needs the actual object to compare the owner ID. Saving to the database happens **after**, only if the check passes.

`Results.Forbid()` returns `403 Forbidden` when the user is authenticated but not permitted. This is different from `Results.Unauthorized()` (`401`), which signals that the caller is not authenticated at all.

---
### Registering the Handler

Authorization handlers are registered with the DI container as `IAuthorizationHandler`. The framework discovers all registered handlers automatically when `AuthorizeAsync` is called.

```csharp
builder.Services.AddSingleton<IAuthorizationHandler, BasketAuthorizationHandler>();
```

`Singleton` is appropriate here because the handler holds no request-specific state; it reads from the context and resource passed in by the framework each time.

---
### Authentication Schemes

A real application often needs more than one way of proving who a user is. A browser session might use cookies, the same app's API might accept JWTs, an internal admin tool might rely on Windows authentication, and a future migration might add a second identity provider alongside the first. If the framework only understood one authentication approach, each of these would need its own parallel pipeline with no consistent way for an endpoint to say "I require *this* kind of authentication."

In ASP.NET Core, an **authentication scheme** is a named pairing of a **handler** (code that examines a request and decides who the user is) and its **options** (issuer URL, signing keys, cookie name, etc.). Every authentication operation is dispatched to a scheme **by name**, so the application can register as many schemes as it needs and route work to the right one per request.

**The five operations a scheme can perform:**
- **Authenticate**: read the request and produce a `ClaimsPrincipal` if credentials are valid
- **Challenge**: tell an unauthenticated caller how to authenticate (e.g. return `401`, or redirect to a login page)
- **Forbid**: tell an authenticated caller they are not allowed (e.g. return `403`)
- **Sign-in**: create a session for the user (e.g. issue a cookie)
- **Sign-out**: end the session

Not every handler implements all five: a JWT bearer handler authenticates and challenges but never signs in (the identity provider issues the token), while a cookie handler does all of them.

**Every scheme has a name.** Each handler type ships an `Add{Scheme}` extension method that registers a scheme. Both overloads exist:

```csharp
.AddJwtBearer()                      // name = JwtBearerDefaults.AuthenticationScheme ("Bearer")
.AddJwtBearer("Keycloak", options => /* ... */)  // name = "Keycloak"
```

The no-argument overload uses the handler's default constant (`"Bearer"`, `"Cookies"`, `"OpenIdConnect"`, ...) so trivial apps do not have to think about names. The name only becomes load-bearing when the application needs **two schemes of the same handler type**, e.g. one JWT bearer for Keycloak and another for an external identity provider, because both cannot use the same default name.

To keep names out of magic strings, the convention is a static `Schemes` class, following the same pattern as `Roles` and `Policies` elsewhere in this project:

```csharp
public static class Schemes
{
    public const string Keycloak = nameof(Keycloak);
}
```

**Default schemes:**
`AddAuthentication(defaultScheme)` sets the default scheme used for every operation when an endpoint does not specify one. For finer control, the options object exposes separate defaults per operation:

- `DefaultAuthenticateScheme`: which scheme runs to identify the user on each request
- `DefaultChallengeScheme`: which scheme handles an unauthenticated request
- `DefaultForbidScheme`: which scheme handles an authenticated-but-forbidden request
- `DefaultSignInScheme` / `DefaultSignOutScheme`: which scheme issues / clears the session

A typical web app combining cookies and OpenID Connect uses **cookies** as `DefaultAuthenticateScheme` (read the session cookie on every request) and **OIDC** as `DefaultChallengeScheme` (redirect to the identity provider when unauthenticated):

```csharp
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options => { /* issuer, client id, ... */ });
```

**Per-endpoint override:**
An endpoint can ignore the default and accept a specific scheme (or list of schemes):

```csharp
app.MapGet("/api/data", () => "ok")
   .RequireAuthorization(new AuthorizeAttribute
   {
       AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme
   });
```

This is how one application hosts both cookie-authenticated pages and token-authenticated APIs: the default is cookies, but selected endpoints demand `"Bearer"`.

**In this project (`Shared/Authorization/AuthorizationExtensions.cs`):**
The API registers two JWT bearer schemes, an unnamed default plus a named `"Keycloak"` scheme, wrapped in an extension method so `Program.cs` stays a list of one-line composition calls.

```csharp
public static IHostApplicationBuilder AddGoGameShopAuthentication(
    this IHostApplicationBuilder builder)
{
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
    return builder;
}
```

`Program.cs` then just calls it:
```csharp
builder.AddGoGameShopAuthentication();
builder.AddGoGameShopAuthorization();
```

Line by line:

- `AddAuthentication(Schemes.Keycloak)`: sets `"Keycloak"` as the default scheme for every operation. Endpoints not naming a scheme will be authenticated by the Keycloak handler.
- `AddJwtBearer(options => ...)`: registers the **unnamed** JWT bearer scheme (under the default `"Bearer"` name). No `Authority` or `Audience`, so it acts as a generic fallback.
- `AddJwtBearer(Schemes.Keycloak, options => ...)`: registers a **second** JWT bearer scheme under the `"Keycloak"` name with provider-specific settings. `Authority` and `Audience` are not hardcoded here; they are auto-bound from configuration (next section).
- `RequireHttpsMetadata = false`: allows fetching the OIDC discovery document over HTTP, required for local Keycloak on `http://localhost:8080`. Never disable this in production.
- `OnTokenValidated`: runs the `KeycloakClaimsTransformer` on every valid token to reshape the claims for this project (covered in the claims transformation section below).

---
### Binding JWT Options from Configuration

Hardcoding `options.Authority = "http://localhost:8080/..."` and `options.Audience = "gogameshop-api"` in `Program.cs` glues the API to one environment at compile time. Production needs a different authority URL, staging another, and shipping localhost URLs in the binary is a smell.

Since .NET 8, `Microsoft.AspNetCore.Authentication.JwtBearer` automatically binds `JwtBearerOptions` from `Authentication:Schemes:{SchemeName}` in the configuration system. As long as the JSON keys match `JwtBearerOptions` properties (or their `TokenValidationParameters` properties), values flow in without any `builder.Configuration.Bind(...)` glue. Set them in the JSON and they show up on `options` at runtime; overrides per environment work the same way `appsettings` always does.

**In `appsettings.json` (committed defaults):**
```json
"Authentication": {
  "Schemes": {
    "Keycloak": {
      "ValidAudience": "gogameshop-api",
      "Authority": "http://localhost:8080/realms/gogameshop"
    }
  }
}
```

**In `appsettings.Development.json` (env-specific overrides):**
```json
"Authentication": {
  "Schemes": {
    "Keycloak": {
      "ValidAudience": "gogameshop-api",
      "Authority": "http://localhost:8080/realms/gogameshop"
    }
  }
}
```

**Two pitfalls worth knowing:**

1. **The section name must match the scheme name exactly.** If `AddJwtBearer(Schemes.Keycloak, ...)` registers a scheme called `"Keycloak"`, the JSON key must be `Keycloak`, not `Bearer` or `JwtBearer`. The default unnamed scheme is `"Bearer"`, so the dotnet user-jwts template uses that key: copy-pasting that template under a named scheme silently leaves the named scheme unconfigured.

2. **`ValidAudience` (singular) vs `ValidAudiences` (plural) differ in type.** The singular binds to a `string`. The plural binds to `IEnumerable<string>` and *requires a JSON array*: passing a bare string under the plural key silently leaves the property empty, and audience validation fails with `IDX10214: Audience validation failed ... validationParameters.ValidAudience: 'null' or validationParameters.ValidAudiences: 'empty'`. Use `"ValidAudience": "gogameshop-api"` for one audience, `"ValidAudiences": ["a", "b"]` only when there are multiple.

**Why not put everything in `appsettings.Development.json`?**
Either pattern works, but only one should hold the "real" values to avoid confusion:
- Keep dev defaults in `appsettings.json`, override per-env in `appsettings.Production.json` (and friends): pragmatic for a learning project, no hidden state.
- Keep `appsettings.json` empty of env-specific keys, put each env's values in its `appsettings.{Env}.json`: cleaner separation, no localhost leaking into prod by accident.

The project currently duplicates the same keys in both files; pick one when adding a real production config.

---
### Custom ClaimTypes Constant

The JWT `role` and `scope` claim names are referenced in multiple places: `RoleClaimType` configuration, `RequireClaim(...)` calls in policies, the claims transformer, etc. Hardcoding `"role"` and `"scope"` everywhere is the same magic-string problem that `Roles` and `Policies` classes solved. Additionally, `System.Security.Claims.ClaimTypes` already exists in .NET and uses long URL-style claim names that don't match the short names in JWTs from external providers.

A project-specific `ClaimTypes` class with `const string` fields holds the short JWT claim names used by the identity provider:

```csharp
namespace GoGameShop.Api.Shared.Authorization;

public static class ClaimTypes
{
    public const string Role = "role";

    public const string Scope = "scope";
}
```

This shadows the framework's `System.Security.Claims.ClaimTypes`, which is intentional. The project uses short claim names (`role`, `sub`, `scope`) from the JWT spec, not the long Microsoft-style URLs.

---
### Claims Transformation — Splitting the `scope` Claim

The OAuth 2.0 spec defines the `scope` claim as a **single space-separated string**: `"openid profile gogameshop_api.all"`. ASP.NET Core's `RequireClaim("scope", "gogameshop_api.all")` does an exact-match comparison against a claim's value, so it sees the whole string and never matches a single scope inside it. Authorization built on individual scopes breaks unless something splits the string into one claim per scope first.

`KeycloakClaimsTransformer` is a small project-owned class with a `Transform(TokenValidatedContext)` method. It is invoked from `JwtBearerEvents.OnTokenValidated` once per token validation. It pulls the single `scope` claim off the principal, splits it on spaces, and re-adds one `scope` claim per individual scope. After it runs, `RequireClaim(ClaimTypes.Scope, "gogameshop_api.all")` matches as expected.

**In code (`Shared/Authorization/KeycloakClaimsTransformer.cs`):**
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

ASP.NET Core ships an `IClaimsTransformer` interface for exactly this kind of post-authentication claim shaping, and the natural instinct is to use it. But its lifecycle is wrong for splitting the `scope` claim, and using it the wrong way leads to either duplicated claims or wasted CPU on every request.

`IClaimsTransformer.TransformAsync` runs **on every authenticated request**, not once when the token is validated. The framework also documents that it may run multiple times for the same principal, so any implementation must be **idempotent**: running it twice must produce the same result as running it once. Splitting `"openid profile gogameshop_api.all"` once gives you three `scope` claims; running the same splitter again on a principal that now has three `scope` claims would either duplicate them or require an extra "have I already split?" check on every request.

The custom class wins here for several reasons:

- **Runs once, not per request.** `OnTokenValidated` fires when the JWT middleware first builds the `ClaimsPrincipal` from the token. After that, the same principal is reused for the lifetime of the authentication; no further work is needed on subsequent requests.
- **No idempotency burden.** Because it runs exactly once per token, the splitter doesn't have to detect and skip its own previous output.
- **Scoped to the Keycloak scheme.** `Events` is set on the named `Schemes.Keycloak` JWT bearer registration, so the transformation only applies to tokens from that scheme. An `IClaimsTransformer` is global; it runs against every authenticated principal regardless of scheme.
- **Plain class, plain DI.** It's just a class with a `Transform` method. No interface contract, no `ClaimsPrincipal` cloning that `IClaimsTransformer` implementations are expected to do, no ceremony.

The naming is deliberate: `KeycloakClaimsTransformer` describes the *purpose*, not the framework interface it implements. It deliberately does not implement `IClaimsTransformer`.

---
### Applying Policies to Endpoints

A registered policy is just a definition; it doesn't enforce anything until something attaches it to a route. The fallback covers the default case, but anything stricter (like admin-only endpoints) needs an explicit hookup at the call site.

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
