## OAuth 2.0 and OpenID Connect

### Why this exists

**The original problem:**
A user has an account on service A (Keycloak). A different application B (Postman, GoGameShop API, a mobile app) needs to know who they are or act on their behalf. The naive solution is to ask the user for their service A password and store it. That's terrible — application B now sees the password, can do *anything* the user can do, and there's no way to revoke just B's access.

**OAuth 2.0's answer:**
Instead of sharing the password, the user logs in **directly** with service A. Service A then hands application B a **token** — a short-lived, narrowly-scoped credential that says "the bearer of this token is allowed to do X on behalf of user Y until time Z." Application B never sees the password.

**OpenID Connect's addition:**
OAuth 2.0 was designed for *authorization* (granting access). It deliberately doesn't say *who the user is* — it just hands out access tokens. **OpenID Connect (OIDC)** is a thin layer on top of OAuth 2.0 that adds an **ID token**: a signed JWT containing identity claims (`sub`, `email`, `name`, etc.). With OIDC, the same flow that grants API access also tells the client *who logged in*.

Keycloak speaks both. The GoGameShop API uses the OIDC layer (it cares about identity — who owns this basket?), and Postman uses the OAuth flow to get a token to send to the API.

---
### The four roles in every flow

Every OAuth/OIDC interaction has exactly four roles. Get these straight and the rest of the spec stops feeling magical:

| Role | In this project | Job |
|---|---|---|
| **Resource Owner** | The human user | Owns the data, grants permission |
| **Client** | Postman | Wants to call the API on the user's behalf |
| **Authorization Server** | Keycloak | Authenticates the user, issues tokens |
| **Resource Server** | GoGameShop API | Holds the data, validates tokens |

The whole protocol is choreography between these four. Postman never talks to the user's password. The API never talks to Keycloak's user database. Each role only knows what it strictly needs to.

---
### The grant types (flows)

OAuth 2.0 defines several **grant types** — different choreographies for different client situations. Pick the wrong one and you either leak credentials or build something you can't ship.

#### Authorization Code Flow

**Who it's for:** Server-side web apps that can keep a secret (a Node/.NET/Rails backend). The "confidential client" case.

**The choreography:**
1. App redirects the user's browser to Keycloak's `/auth` endpoint
2. User logs in to Keycloak directly
3. Keycloak redirects the browser back to the app with a short-lived `code` in the URL
4. The app's **backend** (not the browser) exchanges that code + its client secret at Keycloak's `/token` endpoint for an access token

**Why two steps (code, then token)?** The code is exposed in the browser URL — anyone shoulder-surfing or reading server logs could see it. But the code is useless without the client secret, which only the backend knows. So even if the code leaks, no one can redeem it.

#### Authorization Code Flow with PKCE

**Who it's for:** Public clients — single-page apps, mobile apps, desktop apps, **Postman**. Anything where you can't ship a client secret because the code is on the user's machine and can be inspected.

**The problem PKCE solves:** Without a client secret, the plain Authorization Code flow falls apart. If anyone can intercept the code from the redirect, anyone can exchange it for a token. PKCE plugs the hole using a one-time secret generated per request.

**The choreography (with PKCE additions in bold):**
1. **Postman generates a random `code_verifier` (a long random string) and a `code_challenge` (the SHA-256 hash of the verifier).**
2. Postman opens the browser to Keycloak's `/auth` endpoint with the `code_challenge` attached.
3. User logs in.
4. Keycloak redirects back with a code; **Keycloak also stored the `code_challenge` against this code internally.**
5. Postman exchanges the code at `/token` — **but this time it also sends the original `code_verifier`.**
6. Keycloak hashes the verifier, checks it matches the challenge it stored, then issues the token.

An attacker who intercepts the code can't redeem it without the verifier, which never left Postman.

**This is the flow used in this project** — Postman's OAuth 2.0 helper is configured for "Authorization Code (with PKCE)" against the `gogameshop-postman` client.

#### Client Credentials Flow

**Who it's for:** Machine-to-machine — a backend job, a cron task, a worker calling another service. **No human user is involved.**

**The choreography:**
1. The client (e.g., a worker) sends its `client_id` + `client_secret` directly to `/token`
2. Keycloak issues an access token tied to the client itself, not a user

**When you'd use it here:** Not yet — the API only has user-facing endpoints. But if a future scheduled job needed to call the API to clean up baskets, it would use this flow with its own dedicated client.

#### Resource Owner Password Credentials (ROPC)

**Who it's for:** Effectively nobody anymore. The client collects the user's username and password directly and sends them to `/token`.

**Why it exists:** Legacy migrations from old systems where users were already typing passwords into a first-party client.

**Why to avoid it:** It defeats the entire point of OAuth — the client sees the password. No SSO, no MFA prompt, no consent screen. The OAuth 2.1 draft removes it entirely. **Don't use it.** It's worth knowing only so you can recognize and reject it.

---
### PKCE methods

When Postman generates the code challenge, it picks a hashing method and tells Keycloak which one via the `code_challenge_method` parameter:

| Method | What it does | Use it? |
|---|---|---|
| `S256` | `code_challenge = BASE64URL(SHA256(code_verifier))` | **Always** |
| `plain` | `code_challenge = code_verifier` (no hashing) | No — defeats the purpose |

`plain` exists only for clients that genuinely can't compute SHA-256 (a vanishingly small set in 2026). Modern Keycloak clients can be configured to **require S256** — under *Clients → gogameshop-postman → Advanced → Proof Key for Code Exchange Code Challenge Method*, set it to `S256` and Keycloak will reject any token request that uses `plain`.

---
### Client authentication methods

When a client redeems a code (or uses Client Credentials), it has to prove it is who it claims to be. Keycloak supports several methods, configured per-client under *Clients → … → Credentials → Client Authenticator*:

| Method | How the client authenticates | Used for |
|---|---|---|
| **Client Secret (Basic)** | `Authorization: Basic base64(clientId:secret)` header on `/token` | Confidential clients (server-side apps) |
| **Client Secret (Post)** | `client_id` and `client_secret` in the form body | Same as above; chosen for proxies that strip Basic |
| **None (public)** | No secret. Relies on PKCE for security. | Postman, SPAs, mobile apps |
| **Signed JWT (private_key_jwt)** | Client signs a JWT with its private key; Keycloak verifies with the registered public key | High-security M2M; avoids shared secrets |

**For `gogameshop-postman`:** Client Authentication is **off** (public client) because Postman can't safely store a secret. PKCE carries the security weight instead.

**For a future server-side client** (say a Next.js frontend with a backend): Client Authentication **on**, method = Client Secret Basic. The secret lives on the server.

---
### Client scopes, mappers, and audience

These three Keycloak concepts decide **what ends up inside the token**. Skip them and the token won't have the claims the API expects.

#### Scope (the request)

A **scope** is a string the client requests at the `/auth` endpoint via the `scope` parameter, e.g. `openid profile email`. It's the client saying "I want a token that lets me see *this much* about the user."

The `openid` scope is special — its presence is what turns an OAuth flow into an OIDC flow. Without `openid`, Keycloak issues an access token but no ID token.

`profile` and `email` are standard OIDC scopes that ask Keycloak to include profile claims (`name`, `preferred_username`) and the email claim, respectively.

#### Client scope (the bundle)

In Keycloak, **Client Scopes** (the noun) are reusable bundles of **mappers** and **roles** that get attached to clients. When a request comes in with `scope=openid profile email`, Keycloak finds the client scopes named `profile` and `email` (the `openid` one is built-in) and pulls in their mappers.

Two flavors per client:
- **Default client scopes** — always applied, regardless of what the client requested
- **Optional client scopes** — only applied when the client explicitly asks via `scope=...`

You can create custom client scopes too — e.g., a `gogameshop-api` scope that adds the audience claim and a custom roles mapper. That keeps the per-client configuration tidy: any client that needs to call the API just attaches the `gogameshop-api` scope.

#### Mappers (the translators)

A **mapper** takes data from Keycloak's user database (or session) and turns it into a claim inside the token. Examples:

| Mapper type | Source | Resulting claim |
|---|---|---|
| User Property | `username` | `preferred_username` |
| User Attribute | custom attribute `department` | `department` |
| User Realm Role | role assignments | `realm_access.roles` array |
| Audience | hardcoded value | `aud` (audience) |
| Hardcoded Claim | static value | whatever you name it |

Without mappers, the JWT would just contain a few standard fields (`sub`, `iat`, `exp`) and not much else. The mappers are what make the token useful to a downstream API.

#### Audience (the `aud` claim)

The **audience** (`aud`) tells the resource server "this token was issued for *you*." A well-behaved API rejects tokens whose `aud` doesn't include its own identifier — otherwise a token issued for a low-privilege service could be replayed against a high-privilege one.

Keycloak handles this with an **Audience Mapper** (under a client scope). For this project, the API expects `aud` to include `gogameshop-api`. The mapper is configured to:
- *Included Client Audience:* `gogameshop-api` (or *Included Custom Audience:* if it's not a registered client)
- *Add to access token:* on

Then `AddJwtBearer` in the API validates `TokenValidationParameters.ValidAudience = "gogameshop-api"`.

---
### The OpenID Connect endpoints

Every OIDC server exposes the same well-known set of endpoints. Keycloak puts them under `/realms/{realm}/protocol/openid-connect/`. The discovery document at `/.well-known/openid-configuration` lists them all — that's how `AddJwtBearer` finds the public keys without being told their URL explicitly.

#### Authorization endpoint

**URL:** `http://localhost:8080/realms/gogameshop/protocol/openid-connect/auth`

**Used for:** The browser-facing step. Postman opens this URL in a browser; Keycloak shows the login page; after login, Keycloak redirects back to the client.

**Request parameters (sent as query string):**

| Parameter | What it does | Example |
|---|---|---|
| `response_type` | What the client wants back. `code` for Authorization Code flow. | `code` |
| `client_id` | Which Keycloak client is asking | `gogameshop-postman` |
| `redirect_uri` | Where Keycloak should send the browser back to. **Must exactly match a Valid Redirect URI registered on the client.** | `https://oauth.pstmn.io/v1/browser-callback` |
| `scope` | What the client is asking for | `openid profile email` |
| `state` | Random string echoed back unchanged. Client checks it matches to prevent CSRF. | `xY9k…` |
| `code_challenge` | PKCE: SHA-256 hash of the verifier | `E9Melhoa2OwvFr…` |
| `code_challenge_method` | PKCE method | `S256` |
| `nonce` | OIDC: random value embedded in the ID token. Client checks it matches to prevent token replay. | `aB3z…` |

**The Postman testing trick — `https://oauth.pstmn.io/v1/browser-callback`:**
Desktop Postman can't directly receive a browser redirect (it's not a web server). So Postman runs a **hosted callback page** at `https://oauth.pstmn.io/v1/browser-callback`. The flow is:

1. Postman builds the `/auth` URL with `redirect_uri=https://oauth.pstmn.io/v1/browser-callback`
2. Browser opens, user logs in to Keycloak
3. Keycloak redirects the browser to `https://oauth.pstmn.io/v1/browser-callback?code=…&state=…`
4. The Postman callback page captures the code from the URL and uses a deep link (`postman://...`) to hand it to the desktop client
5. The desktop client now has the code and continues to the token endpoint

That's why the redirect URI **must be `https`** (Keycloak rejects `http` for non-localhost), and why **it must be registered in the client's Valid Redirect URIs** — Keycloak refuses to redirect to anything not on that list, as a defense against token theft via attacker-chosen redirects.

#### Token endpoint

**URL:** `http://localhost:8080/realms/gogameshop/protocol/openid-connect/token`

**Used for:** The back-channel step. The client POSTs to this endpoint with `application/x-www-form-urlencoded` and gets tokens in JSON back. The browser is *not* involved.

**Request parameters (form-encoded body):**

| Parameter | What it does |
|---|---|
| `grant_type` | Which flow. `authorization_code` here; would be `refresh_token` or `client_credentials` for those flows. |
| `code` | The code the client just received from `/auth` |
| `redirect_uri` | Must match the one used in the `/auth` request (anti-mix-up check) |
| `client_id` | Same client as before |
| `code_verifier` | PKCE: the original random string. Keycloak hashes it and verifies it matches the challenge it stored. |

**Response:**

```json
{
  "access_token": "eyJhbGciOi...",
  "expires_in": 300,
  "refresh_token": "eyJhbGciOi...",
  "refresh_expires_in": 1800,
  "token_type": "Bearer",
  "id_token": "eyJhbGciOi...",
  "scope": "openid profile email"
}
```

#### Access token

A short-lived (5 minutes by default in Keycloak dev mode) signed JWT. The client sends it on every API request as `Authorization: Bearer <token>`. The API validates the signature against Keycloak's public keys (fetched from the discovery doc), checks expiry and audience, and trusts the claims inside.

The access token is **for the API, not the client**. Postman shouldn't try to read claims out of it — those claims are meant for the resource server.

#### Refresh token

A longer-lived token (30 minutes by default in dev) that the client can exchange at `/token` for a fresh access token, **without making the user log in again**. The exchange uses `grant_type=refresh_token`:

```
POST /token
grant_type=refresh_token
refresh_token=eyJhbGciOi...
client_id=gogameshop-postman
```

Postman handles refresh automatically when configured: when an access token is about to expire, it calls `/token` with the refresh token and replaces the cached access token transparently.

#### ID token (OIDC only)

A JWT that **identifies the user to the client** (not to the API). Contains `sub`, `email`, `name`, etc. The client reads this to know who logged in. The API doesn't care about the ID token — it has its own access token to validate.

This is the single biggest difference between OAuth and OIDC: OAuth gives you an access token (good for API calls); OIDC additionally gives you an ID token (good for "show the user's name in the top-right").

---
### End-to-end: what happens when Postman talks to Keycloak

Putting it all together. Here's exactly what flows where when you click **Get New Access Token** in Postman with the configuration from `notes/10-postman.md`.

**Postman config — and what each field is for:**

| Postman field | Value in this project | What it actually controls |
|---|---|---|
| Grant Type | Authorization Code (with PKCE) | Picks the flow — tells Postman to use `response_type=code` and generate a PKCE verifier/challenge |
| Auth URL | `{{keycloakUrl}}/realms/{{realm}}/protocol/openid-connect/auth` | Where to point the browser in step 1 |
| Access Token URL | `{{keycloakUrl}}/realms/{{realm}}/protocol/openid-connect/token` | Where to POST the code in step 4 |
| Client ID | `gogameshop-postman` | Sent as `client_id` on both endpoints; must match a Keycloak client |
| Client Secret | *(blank)* | Empty because the Keycloak client is **public** (Client Authentication: off) |
| Callback URL | `https://oauth.pstmn.io/v1/browser-callback` | Sent as `redirect_uri`; must be in Keycloak's Valid Redirect URIs list |
| Scope | `openid profile email` | Sent as `scope`; `openid` triggers OIDC (you get an ID token), `profile`/`email` pull in extra claims |
| Code Challenge Method | `SHA-256` | PKCE method; sent as `code_challenge_method=S256` |
| Client Authentication | Send as Basic Auth header | Irrelevant for a public client (no secret), but it's the right setting for confidential clients |

**Keycloak config — and why each setting matches:**

| Keycloak setting (Clients → gogameshop-postman) | Value | Why |
|---|---|---|
| Client type | OpenID Connect | We want OIDC tokens (ID token + access token) |
| Client authentication | Off (public) | Postman can't keep a secret; PKCE replaces it |
| Standard flow | On | Enables Authorization Code |
| Direct access grants | Off | We don't want ROPC available |
| Service accounts | Off | We don't want Client Credentials available for this client |
| Valid redirect URIs | `https://oauth.pstmn.io/v1/browser-callback` | Whitelists the Postman callback so Keycloak will redirect there |
| Web origins | (often `+`) | CORS — only relevant if the auth request comes from a browser-side JS client; harmless here |
| Advanced → PKCE Code Challenge Method | `S256` | Forces PKCE; rejects `plain` and missing PKCE |

**The actual exchange, step by step:**

**Step 1 — Postman prepares.** It generates a random `code_verifier` (kept locally) and computes `code_challenge = SHA256(code_verifier)`. It builds an authorization URL:

```
http://localhost:8080/realms/gogameshop/protocol/openid-connect/auth
  ?response_type=code
  &client_id=gogameshop-postman
  &redirect_uri=https://oauth.pstmn.io/v1/browser-callback
  &scope=openid%20profile%20email
  &state=<random>
  &code_challenge=<sha256-of-verifier>
  &code_challenge_method=S256
```

**Step 2 — Browser opens.** Keycloak's login page renders. The user types their Keycloak username and password (the ones set up in `notes/09-keycloak.md` under *Creating Users*). Keycloak validates them against its database.

**Step 3 — Keycloak redirects back.** On success, Keycloak generates a short-lived `code`, **stores the `code_challenge` against it internally**, and redirects the browser:

```
HTTP/1.1 302 Found
Location: https://oauth.pstmn.io/v1/browser-callback?code=abc123…&state=<random>
```

The Postman callback page receives the code and forwards it via deep link to the desktop client.

**Step 4 — Postman exchanges the code.** Now back-channel (no browser). Postman POSTs to `/token`:

```
POST /realms/gogameshop/protocol/openid-connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=authorization_code
&code=abc123…
&redirect_uri=https://oauth.pstmn.io/v1/browser-callback
&client_id=gogameshop-postman
&code_verifier=<the-original-verifier>
```

**Step 5 — Keycloak validates and issues tokens.** Keycloak:
- Looks up the code → finds the stored `code_challenge`
- Hashes the supplied `code_verifier` and confirms it matches
- Confirms `redirect_uri` matches the one used in step 1
- Confirms `client_id` exists and (since this is a public client) doesn't require a secret
- Generates the access token, refresh token, and (because `openid` was in scope) the ID token
- Runs all configured **mappers** to populate claims (roles, email, audience, …)
- Signs the tokens with the realm's RS256 private key
- Returns them as JSON

**Step 6 — Postman caches the tokens.** The dialog closes. Postman now has `{{accessToken}}` available for any request in the collection. Every subsequent request adds `Authorization: Bearer <accessToken>` automatically.

**Step 7 — API call.** Postman sends `GET /baskets/{userId}` with the Bearer header. The API:
- Fetches Keycloak's public keys from `http://localhost:8080/realms/gogameshop/protocol/openid-connect/certs` (cached after first call)
- Verifies the JWT signature
- Checks `exp` (not expired), `iss` (matches `Authority`), `aud` (matches `gogameshop-api`)
- Extracts the `sub` claim (user UUID) and `realm_access.roles` (for `[Authorize(Roles = "Admin")]`)
- Runs the `BasketAuthorizationHandler` to confirm `sub` matches the `{userId}` in the URL
- Returns the basket (or 403)

**Where each Postman field shows up on the wire:**

- `Auth URL` → the URL the browser opens in step 1
- `Client ID` → the `client_id` query param in step 1 and form param in step 4
- `Callback URL` → the `redirect_uri` param in steps 1 and 4
- `Scope` → the `scope` query param in step 1; controls which mappers run in step 5
- `Code Challenge Method` → the `code_challenge_method` param in step 1
- `Access Token URL` → the URL Postman POSTs to in step 4
- `Client Secret` → would be the Basic Auth header in step 4 (empty here because public client)

Once that mapping clicks, every Postman field has an obvious purpose — it's either picking *where* to send a request, *what* to send, or *who* the request is from.

---
