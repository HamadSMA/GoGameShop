## Microsoft Entra

### Introduction

Every application that needs login screens, password storage, MFA, social logins, and token issuance ends up reinventing the same identity stack. Microsoft's answer is **Microsoft Entra**, a hosted identity and access management service that authenticates users and issues signed JWTs that other applications trust. Where Keycloak is self-hosted (you run the container, manage the volume, patch the version), Entra is a software-as-a-service offering hosted by Microsoft. There is nothing to install: you create a tenant in the portal and the identity provider is live.

Entra is the rebranded umbrella over what used to be **Azure Active Directory (Azure AD)**. The brand changed in 2023; the technology underneath is the same OpenID Connect / OAuth 2.0 provider that has been around for years. Entra speaks the same OIDC discovery, JWKS, and v2 token shape that any other compliant identity provider speaks, so the API-side wiring (JWT bearer scheme, `Authority`, `ValidAudience`) is nearly identical to Keycloak's. The differences are in claim names (`scp` vs `scope`, `roles` vs `role`, `oid` vs `sub`) and in the portal configuration, not in the protocol.

---
### External ID vs Workforce Tenants

Entra ships in two flavours, and picking the wrong one wastes a lot of clicking through the portal.

- **Entra ID (workforce tenant)**: aimed at employees and business-to-business scenarios. Comes with Microsoft 365, Teams, Intune, etc. Users sign in with their corporate identity. Not the right fit for an e-commerce site whose users are customers.
- **Entra External ID (customer / CIAM tenant)**: aimed at customer-facing apps. Users sign up with their own email, social accounts, or federated identities; the tenant is dedicated to the application's end users and is separate from any workforce tenant. This is what a shop like GoGameShop wants.

This project uses an External ID tenant. The token issuer URL it produces has a distinctive `ciamlogin.com` host, which the multi-scheme selector keys off when deciding whether to forward an incoming request to the Entra or Keycloak handler.

---
### Creating an External ID Tenant

A tenant is the top-level identity boundary inside Entra: it owns its own users, application registrations, scopes, roles, and signing keys. One tenant per logical product or environment is the norm.

**Steps (Azure portal):**
1. Search for **Microsoft Entra ID** in the portal
2. Open the **Overview** blade, click **Manage tenants** → **Create**
3. Pick **External** (not **Workforce**)
4. Name the tenant (for example `gogameshop`) and pick a region
5. **Create**

What the tenant gives you:
- A **tenant ID** GUID. It appears twice in the issuer URL (`https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0`) and once in the token's `tid` claim.
- A default domain like `gogameshop.onmicrosoft.com`. User sign-in names live under this domain unless a custom domain is added.
- A dedicated signing key pair. Tokens issued by this tenant are only valid for resources that trust this tenant.

---
### Registering the Backend as an Application

Anything Entra issues a token *for* needs to exist as an **application registration** inside the tenant. The registration is what produces the application (client) ID GUID that ends up in every issued token's `aud` claim, and what holds the scopes and app roles the application exposes.

**Steps:**
1. In the tenant, open **App registrations** → **New registration**
2. Name it (for example `gogameshop-api`)
3. Supported account types: **Accounts in this organizational directory only**
4. No redirect URI needed for an API
5. **Register**

The registration page now shows the **Application (client) ID** GUID. This is the value that goes into `appsettings.json` as `ValidAudience`, because Entra puts it in the token's `aud` claim.

---
### Adding API Scopes ("Expose an API")

A **scope** is a named slice of API access that a client can request. Defining one tells Entra "this API has a permission called X that callers may ask for." Clients then request that scope at token-acquisition time, and the granted scopes end up in the token's `scp` claim.

**Steps:**
1. Open the API registration → **Expose an API**
2. Click **Add** next to **Application ID URI** to accept the default URI (typically `api://<client-id>`)
3. **Add a scope**:
   - Scope name: `gogameshop_api.all`
   - Who can consent: **Admins and users**
   - Admin consent display name and description: filled in for the consent prompt
   - State: **Enabled**
4. **Add scope**

The scope is now visible to other application registrations in the tenant as a permission they can request on this API. The string `gogameshop_api.all` is what appears in the issued token's `scp` claim and what the API's `RequireClaim(scope, "gogameshop_api.all")` policy checks for (after the claims transformer splits `scp`).

---
### Registering the Postman Client

The API is one app, but **clients that call the API are separate apps** in Entra. Postman during development is a client; a future SPA frontend would be another. Each needs its own registration with its own client ID. This is the application that ends up in the token's `azp` (authorized party) claim.

**Steps:**
1. **App registrations** → **New registration**
2. Name it (for example `gogameshop-postman`)
3. **Redirect URI**: select **Public client/native (mobile & desktop)** and use Postman's callback URL (`https://oauth.pstmn.io/v1/callback`)
4. **Register**
5. On the new registration, open **Authentication** and confirm **Allow public client flows** is enabled (no client secret involved)

The Postman registration is now a client identity that can request tokens, but it cannot yet call the API: the API permission and consent steps below grant that.

---
### Granting Admin Consent

A client registration that has not been granted permission to call an API will still get a token from Entra, but that token will not carry the requested scope, and every API call will fail with `403`. Consent is the missing step that ties client to API.

**Steps (on the Postman client registration):**
1. Open **API permissions** → **Add a permission**
2. Pick **My APIs** → select the `gogameshop-api` registration
3. Pick **Delegated permissions** → check `gogameshop_api.all` → **Add permissions**
4. Back on the **API permissions** page, click **Grant admin consent for <tenant>** and confirm

The permission row now shows a green check under **Status**. Subsequent tokens issued to the Postman client for this API will carry `gogameshop_api.all` in their `scp` claim. Without this step the scope is silently dropped from the token.

The same pattern repeats for every additional client: a new client registration plus its own permission grant on the API.

---
### Creating Users

The tenant exists, the API is registered, but there is no one to log in. **Users** in an External ID tenant are the identities that authenticate and receive tokens. Their object ID (the GUID in the token's `oid` claim) is the stable per-tenant identifier the API uses for "who is this caller."

**Steps:**
1. In the tenant, open **Users** → **New user** → **Create new user**
2. User principal name (sign-in name): `hamad@<tenant>.onmicrosoft.com`, or a value under a verified custom domain (for example `hamad@gogameshop.com`)
3. Display name: filled in (this becomes the token's `name` claim)
4. Initial password: auto-generated or set manually
5. **Create**

Other ways users land in the tenant:
- **Self sign-up**: user flows let customers register themselves through a hosted sign-up page
- **Social and federated identity**: users sign in with Google, Apple, or another OIDC provider; Entra provisions a local user record automatically
- **B2B invitations**: invite an external user who already has an account in another Entra tenant

What lands in the token for a user:
- `oid`: stable per-tenant object ID GUID (use this as the user identity)
- `sub`: a per-application pairwise pseudonym (different for the same user across different apps; do not use as cross-app identity)
- `preferred_username`: the sign-in name shown in the UI
- `name`: display name
- `tid`: the tenant ID

---
### Defining and Assigning App Roles

Authentication says who the user is; **app roles** say what they are allowed to do. In Entra these are defined on the API's application registration and assigned to users (or other applications) on the tenant's **Enterprise applications** blade.

**Defining an app role (on the API registration):**
1. Open the API registration → **App roles** → **Create app role**
2. Display name: `Admin`
3. Allowed member types: **Users/Groups**
4. Value: `Admin` (this exact string is what appears in the token's `roles` array)
5. Description: filled in
6. **Apply**

**Assigning the role to a user:**
1. Open **Enterprise applications** → find the API's enterprise app entry (same name as the registration)
2. **Users and groups** → **Add user/group**
3. Pick the user → pick the `Admin` role → **Assign**

After this, tokens issued to that user for this API carry:
```json
"roles": ["Admin"]
```

The framework's `RequireRole(Roles.Admin)` finds it, because the Entra scheme registers `RoleClaimType = GoGameShopClaimTypes.Roles` (note the plural, distinct from Keycloak's `role`).

---
### Getting an Access Token from Entra

Postman acquires a token by speaking OAuth 2.0 to the tenant's `/authorize` and `/token` endpoints. Configure the **Authorization** tab of a Postman request (or a collection) with:

- **Type**: OAuth 2.0
- **Grant type**: Authorization Code (with PKCE)
- **Callback URL**: `https://oauth.pstmn.io/v1/callback`
- **Auth URL**: `https://<tenant-id>.ciamlogin.com/<tenant-id>/oauth2/v2.0/authorize`
- **Access Token URL**: `https://<tenant-id>.ciamlogin.com/<tenant-id>/oauth2/v2.0/token`
- **Client ID**: the Postman application's client ID
- **Scope**: `api://<api-client-id>/gogameshop_api.all openid profile`
- **Client Authentication**: **Send client credentials in body**

Clicking **Get New Access Token** opens a browser, redirects to Entra's sign-in page, prompts for the user's credentials, and returns the token to Postman. The token then sits in the request's `Authorization: Bearer ...` header.

The token returned is the JWT explained claim-by-claim in [notes/jwt-tokens.md](jwt-tokens.md). Pasting it into a JWT decoder shows `aud` equal to the API's client ID, `azp` equal to Postman's client ID, `scp` equal to `gogameshop_api.all`, and `roles` containing `Admin` if the user is assigned that role.

---
### Finding the OIDC Metadata Document

The API never needs to know any Entra secret. It needs two strings: the issuer URL (so it can fetch public keys and validate signatures) and the application's own client ID (so it can verify the token was meant for it). Both come from the **OpenID Connect metadata document**.

**To find the metadata URL:**
1. Open the API registration → **Endpoints** (button at the top of the **Overview** blade)
2. Copy the **OpenID Connect metadata document** URL. It looks like `https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0/.well-known/openid-configuration`

`GET` that URL and the response includes:
```json
{
  "issuer": "https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0",
  "authorization_endpoint": "...",
  "token_endpoint": "...",
  "jwks_uri": "...",
  ...
}
```

The `issuer` field is exactly what goes into `appsettings.json` as `Authority`. The framework appends `/.well-known/openid-configuration` itself, fetches this document, and uses `jwks_uri` to download the public keys it caches for signature validation.

**The resulting API configuration:**
```json
"Authentication": {
  "Schemes": {
    "Entra": {
      "ValidAudience": "<api-application-client-id>",
      "Authority": "https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0"
    }
  }
}
```

`Authority` is the token's `iss` claim. `ValidAudience` is the token's `aud` claim. Both values are GUID-bearing strings but they refer to different things: `Authority` identifies the tenant, `ValidAudience` identifies the API application inside that tenant.

The matching backend wiring (the named `Entra` scheme, the `EntraClaimsTransformer`, the multi-scheme `KeycloakOrEntra` policy scheme that picks the right handler per request) is covered in [notes/auth.md](auth.md).
