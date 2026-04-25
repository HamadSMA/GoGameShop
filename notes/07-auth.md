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
### Authorization Policies

**What they are:**
A policy is a named set of requirements that a request must satisfy to be allowed through. Instead of writing `if (!user.HasClaim(...))` inside every endpoint, you define requirements once in a policy and reference the policy by name on each endpoint.

**How it fits — `Program.cs`:**
```csharp
builder
    .Services.AddAuthorizationBuilder()
    .AddPolicy(
        Policies.UserAccess,
        authBuilder =>
        {
            authBuilder.RequireClaim("scope", "gogameshop_api.all");
        }
    )
    .AddPolicy(
        Policies.AdminAccess,
        authBuilder =>
        {
            authBuilder.RequireClaim("scope", "gogameshop_api.all");
            authBuilder.RequireRole(Roles.Admin);
        }
    );
```

`AddAuthorizationBuilder()` — registers the authorization services and returns a builder for chaining policies.

`AddPolicy(name, configure)` — registers a named policy. The `configure` callback receives an `AuthorizationPolicyBuilder` where you attach requirements.

`RequireClaim("scope", "gogameshop_api.all")` — the JWT must contain a claim named `scope` with the value `gogameshop_api.all`. This identifies the token as one issued for this API specifically.

`RequireRole(Roles.Admin)` — the JWT's `role` claim must contain `"Admin"`. Combined with the scope check, `AdminAccess` requires both conditions to be true.

**Two policies in this project:**

| Policy | Requirements | Who passes |
|--------|-------------|------------|
| `UserAccess` | scope = `gogameshop_api.all` | Any authenticated user with a valid API token |
| `AdminAccess` | scope = `gogameshop_api.all` + role = `Admin` | Only users with the Admin role |

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

Endpoints with no `RequireAuthorization` call are public — no token required.

**Endpoint authorization summary:**

| Method | Route | Policy |
|--------|-------|--------|
| `GET` | `/games` | Public |
| `GET` | `/games/{id}` | Public |
| `POST` | `/games` | AdminAccess |
| `PUT` | `/games/{id}` | AdminAccess |
| `DELETE` | `/games/{id}` | AdminAccess |
| `GET` | `/baskets/{userId}` | UserAccess |
| `PUT` | `/baskets/{userId}` | UserAccess |
| `GET` | `/genres` | Public |
| `GET` | `/ratings` | Public |

---
