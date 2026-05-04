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
After login, Keycloak embeds the user's roles in the JWT under `realm_access.roles`. The API maps this claim to ASP.NET Core's role system so `[Authorize(Roles = "Admin")]` and policies like `AdminAccess` (defined in `Shared/Authorization/Policies.cs`) work as expected.

The mapping happens in `Program.cs` via `JwtBearerOptions.TokenValidationParameters.RoleClaimType` — without it, ASP.NET Core looks for a `role` claim that Keycloak doesn't emit by default.

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
