## Authentication & Authorization

### Authentication vs Authorization

**What they are:**
Two separate concepts that are often confused:

- **Authentication** — *Who are you?* Validates the caller's identity by checking a token or credential.
- **Authorization** — *Are you allowed?* Checks whether the identified caller has permission to do what they're asking.

In ASP.NET Core, these are two separate middleware registrations and two separate service registrations. Authentication must run first — authorization depends on it knowing who the caller is.

---
### JWT Bearer Authentication

**What it is:**
JWT (JSON Web Token) is a compact, self-contained token format. A client receives a JWT from an identity provider after logging in and attaches it to subsequent requests via the `Authorization` header:

```
Authorization: Bearer eyJhbGci...
```

The server validates the token's signature and reads the claims embedded inside it — no database lookup needed.

**How it fits — `Program.cs`:**
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

**What it is:**
`AddFallbackPolicy` registers a policy that automatically applies to every endpoint that has no explicit authorization configured. It flips the default: instead of endpoints being public unless locked down, they are locked down unless explicitly opened up.

**Why it matters:**
With `AddPolicy` alone, forgetting `RequireAuthorization` on a new endpoint silently leaves it public. With a fallback, forgetting means the fallback kicks in — a safer default that prevents accidental exposure.

**How it fits — `AuthorizationExtensions.cs`:**
```csharp
builder
    .Services.AddAuthorizationBuilder()
    .AddFallbackPolicy(
        Policies.UserAccess,
        authBuilder =>
        {
            authBuilder.RequireClaim("scope", ApiAccessScope);
        }
    )
    .AddPolicy(
        Policies.AdminAccess,
        authBuilder =>
        {
            authBuilder.RequireClaim("scope", ApiAccessScope);
            authBuilder.RequireRole(Roles.Admin);
        }
    );
```

The basket endpoints have no auth call on them — they are automatically protected by the `UserAccess` fallback.

`AddPolicy` — registers a named policy applied only when explicitly referenced via `RequireAuthorization(policyName)`. `AdminAccess` is still a named policy because it applies to a specific subset of endpoints.

---
### AllowAnonymous — Explicit Opt-Out

**What it is:**
`.AllowAnonymous()` exempts an endpoint from all authorization checks, including the fallback policy. When a fallback policy is active, this is the only way to make an endpoint publicly accessible.

**How it fits:**
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

**What it is:**
By default, ASP.NET Core adds the authorization middleware automatically. But when you need to control where it sits in the pipeline — specifically after `UseStaticFiles()` — you call `app.UseAuthorization()` explicitly.

**Why order matters here:**
Static files (`wwwroot/`) are served by `UseStaticFiles()`. If `UseAuthorization()` runs before it, the authorization middleware would intercept requests for images and other assets and return `401` to anonymous users — even when those files are meant to be public.

**How it fits — `Program.cs`:**
```csharp
app.UseStaticFiles();       // serve wwwroot/ files first, before auth runs
app.UseAuthorization();     // then enforce auth on API endpoints

app.MapGames();
app.MapGetGenres();
// ...
```

Placing `UseAuthorization()` after `UseStaticFiles()` means static file requests short-circuit before authorization is checked, so images load for everyone regardless of auth state.

---
### Roles and Policies — Static Constant Classes

**What they are:**
`Roles` and `Policies` are static classes that hold string constants — the names of roles and policies used throughout the project.

**Why it's used:**
Policy and role names are referenced in at least two places: where they're defined (in `Program.cs`) and where they're applied (on each endpoint). Hardcoding the same string in multiple places means a typo causes a silent runtime failure instead of a compile-time error. A constant means you change it once and the compiler catches any missed references.

**How it fits:**
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
### Applying Policies to Endpoints

**What it is:**
`RequireAuthorization(policyName)` chains onto a Minimal API endpoint registration and tells the framework to enforce a named policy before the handler runs. If the request fails the policy, the framework short-circuits with `401 Unauthorized` (not authenticated) or `403 Forbidden` (authenticated but not authorized).

**How it fits:**
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
