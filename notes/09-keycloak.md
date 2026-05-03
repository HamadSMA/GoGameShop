## Keycloak

### Introduction

**The problem:**
Every application that needs login screens, password storage, password resets, social logins, MFA, and token issuance ends up reinventing the same identity stack — usually badly. Storing passwords correctly, rotating signing keys, and supporting SSO are all easy to get subtly wrong, and getting them wrong is a security incident.

**What it does:**
Keycloak is an open-source **identity and access management (IAM)** server. It centralizes authentication (verifying who a user is) and issues signed tokens that other applications trust to authorize requests — so each app only needs to validate the tokens, not implement the identity stack itself. It speaks **OpenID Connect** (an identity layer on top of OAuth 2.0) and signs tokens with **RS256**, both of which standard JWT middleware understands out of the box.

**In code:**
Keycloak ships as a container image. The compose file in `Backend/localinfra/docker-compose.yml` starts it on port `8080` with bootstrap admin credentials and a persistent volume so users and configuration survive restarts:

```yaml
services:
  keycloak:
    image: quay.io/keycloak/keycloak:26.6.1
    container_name: keycloak
    ports:
      - "8080:8080"
    environment:
      - KC_BOOTSTRAP_ADMIN_USERNAME=admin
      - KC_BOOTSTRAP_ADMIN_PASSWORD=admin
    command: ["start-dev"]
    volumes:
      - keycloak-data:/opt/keycloak/data

volumes:
  keycloak-data:
```

`start-dev` runs Keycloak in development mode — disables HTTPS requirements, uses an embedded H2 database, and skips production-only checks. Not for real deployments.

The admin console lives at `http://localhost:8080`.

---
### Realms and How to Create One

**The problem:**
A single Keycloak instance might host multiple unrelated products, environments, or tenants — each with its own users, roles, and signing keys. Mixing them (or worse, mixing application users with the Keycloak admin account) creates a security and operational mess.

**What it does:**
A **realm** is an isolated tenant inside Keycloak. It owns its own users, roles, clients (applications), groups, and signing keys — users in one realm cannot log into another. The built-in `master` realm is for administering Keycloak itself; application users should never live there. Convention is one realm per logical product or environment — for this project, `gogameshop`.

**Creating one (admin console):**
1. Log in to `http://localhost:8080` as the bootstrap admin
2. Top-left realm dropdown → **Create realm**
3. Name it `gogameshop` → **Create**

Once created, the realm dropdown switches to it and every screen below (Users, Clients, Roles) operates inside that realm.

**What changes per realm:**
- Token issuer URL: `http://localhost:8080/realms/gogameshop`
- OIDC discovery document: `http://localhost:8080/realms/gogameshop/.well-known/openid-configuration`
- Signing keys (each realm has its own key pair)

The API's `Authority` setting in `appsettings.Development.json` points at the realm-specific issuer URL — that's how `AddJwtBearer` finds the public keys to validate tokens.

---
### Creating Users

**The problem:**
The realm exists, but it's empty — there's no one to log in. Before any token can be issued, an identity has to exist somewhere that Keycloak can authenticate against.

**What it does:**
A **user** in Keycloak is the identity that logs in and receives a JWT. Keycloak stores their credentials, profile attributes, role assignments, and session state — the application never sees any of this directly, only the claims that end up in the JWT.

**Steps (admin console):**
1. Inside the `gogameshop` realm → **Users** → **Add user**
2. Fill in `Username` (required), `Email`, `First name`, `Last name`
3. **Create**
4. Open the user → **Credentials** tab → **Set password**
5. Toggle **Temporary** off (otherwise the user is forced to change it on first login — fine for production, annoying for development)
6. **Save password**

**Other ways users get created:**
- **Self-registration** — *Realm settings → Login → User registration: On*. The login page then shows a *Register* link.
- **Identity brokering** — log in with Google/GitHub/etc.; Keycloak provisions a local record automatically.
- **User federation** — point Keycloak at an existing LDAP / Active Directory; users are read from there.

**What's actually stored:**
Username, email, profile attributes, **password hashes** (PBKDF2 by default — never plaintext), role memberships, group memberships, sessions, and federated identity links. The API never reads any of this directly — it only sees the claims that end up in the JWT.

---
### Creating and Assigning Roles

**The problem:**
Authentication tells you *who* the user is, but not *what they can do*. Some users are admins, some are regular customers, and the API needs a way to make that distinction without hardcoding usernames.

**What it does:**
A **role** is a named permission. Keycloak stores roles per realm and embeds them in the user's JWT, so the resource server can authorize based on role membership. Roles come in two flavors:

- **Realm roles** — global within the realm (e.g., `Admin`, `User`). Any client in the realm can see them.
- **Client roles** — scoped to a single client/application. Useful when different apps in the same realm need their own permission models.

Realm roles fit when there's one API and one logical permission model; client roles fit when multiple apps share a realm but each has its own permission set.

**Creating a realm role:**
1. Realm → **Realm roles** → **Create role**
2. Role name: `Admin` → **Save**
3. Repeat for any other roles (e.g., none needed yet — authenticated users without `Admin` are treated as regular customers)

**Assigning a role to a user:**
1. **Users** → pick the user → **Role mapping** tab
2. **Assign role** → filter to *Realm roles* → check `Admin` → **Assign**

**How roles reach the API:**
After login, Keycloak embeds the user's realm roles in the JWT. The API maps that claim to ASP.NET Core's role system so `User.IsInRole("Admin")`, `RequireRole(Roles.Admin)`, and policies like `AdminAccess` (defined in `Shared/Authorization/Policies.cs`) all work as expected.

The mapping happens in `Program.cs` via `JwtBearerOptions.TokenValidationParameters.RoleClaimType = ClaimTypes.Role`, where `ClaimTypes.Role` is the string `"role"`. ASP.NET Core then reads roles from the `role` claim — but **Keycloak doesn't emit a `role` claim by default**. The next section explains how to make it.

---
### Configuring the Roles Claim — `realm_access.roles` → `role`

**The problem:**
Out of the box, Keycloak's built-in `roles` client scope emits realm roles under a **nested** claim — `realm_access.roles` — and client roles under `resource_access.${client_id}.roles`. ASP.NET Core's role checks read a single flat claim name (whatever `RoleClaimType` is set to). The framework cannot navigate into a nested JSON object to find role values, so even though the roles are physically present in the JWT, `User.IsInRole("Admin")` returns `false` and `RequireRole(Roles.Admin)` quietly fails.

**What it does:**
Keycloak builds JWT claims through **protocol mappers** attached to client scopes. The realm-roles mapper has a `Token Claim Name` setting — change it from `realm_access.roles` to `role` and the mapper writes the user's realm roles to a top-level `role` claim instead. Setting `Multivalued = true` writes one JSON array entry per role rather than concatenating them into a single string. ASP.NET Core then unpacks the array into one `role` claim per role, and `RoleClaimType = "role"` finds them.

**Steps (admin console):**
1. **Client scopes** → open the built-in `roles` scope
2. **Mappers** tab → open the **realm roles** mapper
3. Change **Token Claim Name** from `realm_access.roles` to `role`
4. Toggle **Multivalued** **on**
5. Toggle **Add to access token** **on** (it usually already is)
6. **Save**

**What ends up in the access token:**
Before:
```json
{
  "realm_access": { "roles": ["Admin", "default-roles-gogameshop"] }
}
```

After:
```json
{
  "role": ["Admin", "default-roles-gogameshop"]
}
```

`MapInboundClaims = false` in `Program.cs` is what keeps the claim name as the literal string `role` — without it, the JWT middleware would rewrite it to a long Microsoft-style URL and `RoleClaimType = "role"` would no longer match.

> Built-in Keycloak roles like `default-roles-gogameshop`, `offline_access`, and `uma_authorization` will also land in the `role` claim. They're harmless — `RequireRole(Roles.Admin)` only matches `Admin`. Strip them with a custom mapper only if claim size becomes a concern.

---
### Configuring Scopes — Why the API Has to Split the String

**The problem:**
The API's `UserAccess` policy calls `RequireClaim(ClaimTypes.Scope, "gogameshop_api.all")` — it expects to find a `scope` claim whose value is exactly `"gogameshop_api.all"`. But the access token Keycloak issues looks like this:

```json
{
  "scope": "openid profile email gogameshop_api.all"
}
```

`RequireClaim` does an exact-match comparison on the claim value, so it sees the whole space-separated string and never matches the single scope `"gogameshop_api.all"` inside it. Every protected endpoint returns `403 Forbidden`.

**Why Keycloak emits it that way:**
This is **mandated by the OAuth 2.0 spec** (RFC 6749 §3.3 and RFC 8693): the `scope` claim is a single string of space-separated scope names. There is no Keycloak setting that converts it into a JSON array — every spec-compliant authorization server sends scopes as one string. The fix has to happen on the resource server (the API), not on Keycloak.

**The Keycloak side — defining the scope:**
What Keycloak *does* control is which scope names exist and which clients can request them:

1. **Client scopes** → **Create client scope**
2. **Name:** `gogameshop_api.all`
3. **Type:** `Default` (always issued for this client) or `Optional` (only when the client requests it via the `scope` parameter)
4. **Protocol:** `openid-connect`
5. **Include in token scope:** `On` — this is what makes the scope name appear inside the `scope` string of issued tokens
6. **Save**
7. Open the API's client (`gogameshop-api`) → **Client scopes** tab → assign the new scope as Default or Optional

After this, tokens issued for the client carry `gogameshop_api.all` inside the space-separated `scope` value.

**The API side — splitting the string:**
The custom `KeycloakClaimsTransformer` (covered in [notes/07-auth.md](07-auth.md)) runs once per token validation, splits the `scope` claim on spaces, and re-adds one `Claim(ClaimTypes.Scope, ...)` per individual scope. After it runs, the principal carries:

```
scope = "openid"
scope = "profile"
scope = "email"
scope = "gogameshop_api.all"
```

`RequireClaim(ClaimTypes.Scope, "gogameshop_api.all")` now finds a claim whose value matches exactly. This split-on-the-resource-server pattern is the standard approach across all OAuth-protected APIs — it isn't specific to ASP.NET Core or Keycloak.

---
### Export and Import Realm Configurations

**The problem:**
Everything done in the admin console — realm settings, roles, clients, groups, even users — lives in Keycloak's database. Wipe the database, delete the volume, or move to a new machine and the configuration is gone. Worse, sharing a setup with a teammate via a 30-step "click here, then here" guide is brittle and error-prone.

**What it does:**
**Export** captures a realm as a JSON file that can be checked into version control and replayed on any other Keycloak instance. **Import** reads that file at startup and recreates the realm. Together they turn Keycloak configuration into versioned, shareable artifacts instead of ephemeral admin-console state.

**Exporting a realm (admin console, partial export):**
1. Select the realm → **Realm settings** → **Action** menu (top-right) → **Partial export**
2. Choose what to include: groups, roles, clients, etc.
3. **Export** — downloads a JSON file

> Partial export does **not** include users by default — Keycloak considers user data sensitive. Toggle *Include users* if you want them too (rarely a good idea for a file you'll commit).

**Exporting via the CLI (full export, including users):**
```bash
docker exec -it keycloak \
  /opt/keycloak/bin/kc.sh export \
  --dir /opt/keycloak/data/export \
  --realm gogameshop \
  --users realm_file
```

The file lands inside the volume at `/opt/keycloak/data/export/gogameshop-realm.json`. Copy it out with `docker cp keycloak:/opt/keycloak/data/export/gogameshop-realm.json ./`.

**Importing on startup (the practical pattern):**
Keycloak can auto-import a realm file at container startup. Mount the file into `/opt/keycloak/data/import/` and pass `--import-realm`:

```yaml
services:
  keycloak:
    # ... existing config
    command: ["start-dev", "--import-realm"]
    volumes:
      - keycloak-data:/opt/keycloak/data
      - ./gogameshop-realm.json:/opt/keycloak/data/import/gogameshop-realm.json
```

On first boot (with an empty database), Keycloak finds the file and creates the realm. On subsequent boots — if the realm already exists — it skips the import.

**Why this matters for the project:**
The committed `Backend/localinfra/gogameshop-realm.json` is the source of truth for the realm's roles, clients, and settings. Anyone cloning the repo can `docker compose up` and get an identical Keycloak setup. No manual click-through required.

---
