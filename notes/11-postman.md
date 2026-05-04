## Postman

### Introduction

**The problem:**
Exercising a non-trivial API by hand — repeated multipart uploads, role-protected endpoints, OAuth 2.0 token retrieval — gets painful fast with raw curl or `.http` files. Tokens expire, bodies need re-typing, and there's no shared, foldered place to keep request scenarios across machines.

**What it does:**
Postman is a desktop client for sending HTTP requests, organizing them into reusable collections, and exercising APIs end-to-end without writing code. It adds persistent storage, foldered collections, environment variable swapping, and a GUI for tasks like OAuth 2.0 token retrieval that would otherwise require multiple manual steps each time.

**Three components used in this project:**
- **Collections** — saved request definitions, grouped into folders by feature (`games/`, `baskets/`, …)
- **Environments** — variable sets like `{{baseUrl}}`, `{{accessToken}}` that swap per environment
- **Authorization helpers** — built-in OAuth 2.0 flow that handles the browser redirect, captures the auth code, and exchanges it for a token

The `postman/` folder in the repo holds the local workspace files. `.postman/resources.yaml` links the local files to a Postman Cloud workspace so collections sync across machines.

---
### Workspaces and Collections

**The problem:**
A single Postman install can hold dozens of unrelated APIs across personal projects, work, and clients. Without a hierarchy, requests pile into one flat list and nothing is shareable as a unit.

**What a workspace is:**
A **workspace** is the top-level container — usually one per project or team. It owns the collections, environments, mocks, and other resources that belong together. The cloud workspace ID is what `.postman/resources.yaml` records:

```yaml
workspace:
  id: 3d95225a-40a1-417a-879c-b7054b72f48a

cloudResources:
  collections:
    ../postman/collections/GoGameShop.Api - v1: 554f3a1d-112a-4780-9234-e0e622ae4c69
```

This file lets Postman map the local folder structure to the corresponding cloud objects.

**What a collection is:**
A **collection** is an ordered, foldered list of requests. The project's collection — `GoGameShop.Api - v1` — mirrors the API's vertical slices:

```
GoGameShop.Api - v1/
├── games/
│   ├── GET games (paged, search)
│   ├── GET games/{id}
│   ├── POST games (multipart)
│   ├── PUT games/{id} (multipart)
│   └── DELETE games/{id}
├── baskets/
│   ├── GET baskets/{userId}
│   └── PUT baskets/{userId}
├── genres/
│   └── GET genres
└── ratings/
    └── GET ratings
```

**Why folders matter:**
Folders inherit settings from their parent. Auth set on the `baskets/` folder applies to every request inside it, which is convenient for endpoints that all need a bearer token. The collection itself can hold defaults (like the base URL variable) that apply everywhere.

**Local files:**
Each saved request, folder, and environment lives as a YAML/JSON file under `postman/` — committed to the repo if you choose to share them, or gitignored for personal use. Sharing them lets a teammate clone the repo and immediately have a working test set.

---
### Environments and Variables

**The problem:**
The same collection needs to point at `http://localhost:5078` during development, a staging URL on a CI runner, and a production URL during smoke tests. Hardcoding the host in every request means duplicating the collection per environment and updating each one in lockstep.

**What variables are:**
Postman variables are placeholders written `{{name}}` that get substituted at request time. Instead of hardcoding `http://localhost:5078/games` in 20 requests, you write `{{baseUrl}}/games` once and define `baseUrl` in an environment — switching environments rebinds every request at once.

**Environment scope:**
An **environment** is a named group of variables. Switching the active environment (top-right dropdown) changes every request that references its variables. Typical setup for this project:

| Variable | Local | (future) Production |
|---|---|---|
| `baseUrl` | `http://localhost:5078` | `https://api.gogameshop.example.com` |
| `keycloakUrl` | `http://localhost:8080` | `https://auth.gogameshop.example.com` |
| `realm` | `gogameshop` | `gogameshop` |
| `clientId` | `gogameshop-postman` | `gogameshop-postman` |
| `accessToken` | *(set after login)* | *(set after login)* |
| `userId` | *(a Keycloak user UUID)* | *(a Keycloak user UUID)* |

The environment file (e.g. `postman/environments/Local.environment.yaml`) is just a list of name/value pairs.

**Variable types:**
- **Initial value** — default that gets shared with the team via the synced file
- **Current value** — local override that stays on your machine (use this for secrets and tokens)

Always store passwords and tokens as **current value only** — initial values get committed when the file is shared.

**Reference syntax:**
- URL: `{{baseUrl}}/baskets/{{userId}}`
- Header: `Authorization: Bearer {{accessToken}}`
- Body: `{ "userId": "{{userId}}", "items": [...] }`

---
### OAuth 2.0 with Keycloak

**The problem:**
Protected endpoints require a valid JWT — unauthenticated calls get 401. Obtaining one by hand (open browser → log in → copy token from URL → paste into request header → repeat every 5 minutes when it expires) is tedious and error-prone, and copying tokens through clipboards risks leaking them.

**What it does:**
Postman's built-in OAuth 2.0 helper automates the entire token dance: it opens the browser to the authorization server, captures the redirect, exchanges the code, and stores the token internally — ready to be attached to any request in the collection.

**Flow used: Authorization Code with PKCE**
This is the OIDC flow for browser/desktop clients. It works like this:

1. Postman opens a browser pointed at Keycloak's authorize endpoint
2. The user logs in to Keycloak
3. Keycloak redirects back with an authorization code attached to the URL
4. Postman exchanges the code for an access token (and optionally a refresh token)
5. Postman saves the token under whichever variable you nominate (`{{accessToken}}`)

**Configuration in Postman:**
Open the collection (or a folder) → **Authorization** tab → **Type: OAuth 2.0** → **Configure New Token**:

| Field | Value |
|---|---|
| Token Name | `gogameshop-local` |
| Grant Type | Authorization Code (with PKCE) |
| Callback URL | `https://oauth.pstmn.io/v1/browser-callback` |
| Auth URL | `{{keycloakUrl}}/realms/{{realm}}/protocol/openid-connect/auth` |
| Access Token URL | `{{keycloakUrl}}/realms/{{realm}}/protocol/openid-connect/token` |
| Client ID | `{{clientId}}` |
| Client Secret | *(blank — public client)* |
| Code Challenge Method | SHA-256 |
| Scope | `openid profile email` |
| Client Authentication | Send as Basic Auth header |

Click **Get New Access Token** — a browser window opens, Keycloak prompts for credentials, and Postman lands the token back in the dialog. Click **Use Token** to save it.

**The callback URL — `https://oauth.pstmn.io/v1/browser-callback`:**
Postman runs a hosted callback service at this URL because desktop apps can't receive browser redirects directly. The page captures the auth code and forwards it to your Postman client via a deep link. **Note the `https`** — `http` is rejected by Keycloak.

**Registering the callback in Keycloak:**
The callback URL must be in the client's **Valid Redirect URIs** list, otherwise Keycloak responds with `Invalid redirect_uri`:

```
Realm → Clients → gogameshop-postman → Settings →
  Valid Redirect URIs:
    https://oauth.pstmn.io/v1/browser-callback
```

**Where the token goes:**
Postman stores the token internally per-collection. To make it available to scripts and other requests, set **Token Name** and reference it as `{{gogameshop-local}}` — or save it explicitly to an environment variable using a *Tests* script (out of scope here).

---
### Testing Protected Endpoints

**The problem:**
A protected API will reject most calls — but the rejection might mean *no token*, *wrong user*, or *wrong role*, and each requires a different fix. Without a deliberate test plan, it's easy to miss a regression where an endpoint silently becomes more (or less) permissive than intended.

**What protection looks like in this project:**
The API's endpoints fall into three categories:

| Category | Examples | Required identity |
|---|---|---|
| Anonymous | `GET /genres`, `GET /ratings`, `GET /games` | none |
| `UserAccess` policy (fallback) | `GET /baskets/{userId}`, `PUT /baskets/{userId}` | any authenticated user, plus a resource check |
| `AdminAccess` policy | `POST /games`, `PUT /games/{id}`, `DELETE /games/{id}` | authenticated user with `Admin` realm role |

The fallback policy means *every* endpoint requires authentication unless explicitly marked `[AllowAnonymous]`. The `BasketAuthorizationHandler` adds a second check: a non-admin user can only access their own basket — the `userId` in the URL must match the `sub` claim in their JWT.

**Attaching the token:**
With OAuth 2.0 configured at the collection level (previous section), every request inside the collection inherits it. Postman adds the header automatically:

```
Authorization: Bearer eyJhbGciOiJSUzI1NiIs...
```

No copying or pasting required.

**Three scenarios worth testing:**

**1. No token → 401 Unauthorized**
Disable auth on a single request (Authorization tab → No Auth) and send `GET /baskets/{{userId}}`. The API rejects it before the endpoint handler runs.

**2. Wrong user → 403 Forbidden**
Log in as user A but call `GET /baskets/<user-B-uuid>`. The endpoint authenticates fine (the token is valid), but the resource-based handler in `BasketAuthorizationHandler` denies the request.

**3. Wrong role → 403 Forbidden**
Log in as a non-Admin user and call `DELETE /games/{id}`. The token is valid and the user is authenticated, but the `AdminAccess` policy rejects them because the `Admin` realm role is missing.

**Reading 401 vs 403:**

- **401** — *"I don't know who you are."* No token, expired token, invalid signature.
- **403** — *"I know who you are, you're just not allowed."* Wrong role, wrong owner, wrong scope.

The status codes are distinct and worth memorizing — they tell you whether the problem is authentication or authorization, which determines where to look in the code (`AddJwtBearer` config vs policies/handlers).

**Token lifetime:**
Access tokens issued by Keycloak in dev mode last about **5 minutes** (`accessTokenLifespan: 300` in the realm config). After expiry, requests start returning 401. Either click **Get New Access Token** again, or configure Postman to use the refresh token automatically — whichever fits your workflow.

---
