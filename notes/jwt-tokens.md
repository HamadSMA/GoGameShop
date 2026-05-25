## JWT Tokens

### What a JWT Actually Is

A JWT (JSON Web Token) is a compact, URL-safe string carrying a JSON object whose signature can be verified without contacting the issuer. It is three Base64Url-encoded segments joined by dots:

```
<header>.<payload>.<signature>
```

Decoded:
- **Header**: small JSON describing how the token is signed. Typical keys: `typ` (always `"JWT"`), `alg` (signing algorithm such as `RS256`), and `kid` (the ID of the signing key, used to look up the public key in the issuer's JWKS endpoint).
- **Payload**: the JSON object with the claims. Every key in the payload is a claim.
- **Signature**: an RSA or HMAC signature over `<header>.<payload>` produced with the issuer's private key. Anyone holding the matching public key can verify the signature, but only the issuer can produce one.

A JWT is signed, not encrypted. The payload is readable by anyone who has the token. Treat it as a tamper-proof envelope, not a secret.

---
### Who Issues, Who Carries, Who Validates

Three actors are involved in every JWT-secured request, and conflating their roles is the most common source of confusion.

1. **The identity provider (issuer)** holds the signing key. It authenticates the user (password, social login, certificate, etc.) and issues a signed token. Examples: Keycloak, Microsoft Entra, Auth0. The token's `iss` claim names the issuer.
2. **The client** asks the identity provider for a token (the OAuth 2.0 token request) and stores it. The client attaches the token to every API request as `Authorization: Bearer <token>`. Examples: a browser SPA, Postman, a mobile app.
3. **The API (resource server)** validates incoming tokens on every request: signature, expiry, audience, issuer. If validation passes, the API trusts the claims inside the token and uses them for authorization decisions. The API never sees the user's password and never talks to the user; it only sees tokens.

The flow:

```
client ─ login ─▶  identity provider
client ◀ token ─   identity provider
client ─ Authorization: Bearer <token> ─▶  API
                                            API verifies signature against issuer's JWKS
                                            API checks aud, iss, exp
                                            API reads claims, runs authorization rules
client ◀ 200 / 401 / 403 ─                 API
```

The API never calls the identity provider per request. It downloads the issuer's public keys once (via the OIDC discovery document) and caches them, so token validation is a local CPU operation.

---
### Standard JWT Claims

These claims are defined in RFC 7519 and appear in tokens from any conforming issuer. The framework's `TokenValidationParameters` checks several of them automatically.

| Claim | Meaning | Validated by default? |
|-------|---------|-----------------------|
| `iss` | **Issuer.** URL identifying who minted the token. Must match the configured `Authority`. | yes |
| `aud` | **Audience.** Identifier of the API this token is meant for. Must match `ValidAudience`. A token issued for a different API is rejected. | yes |
| `exp` | **Expiry.** Unix timestamp after which the token is no longer valid. The framework rejects expired tokens. | yes |
| `nbf` | **Not before.** Unix timestamp before which the token is not yet valid. Guards against clock-skew replay. | yes |
| `iat` | **Issued at.** Unix timestamp of when the token was minted. Informational; not used for validation but useful in logs. | no |
| `sub` | **Subject.** The unique, stable identifier of the user inside the issuer. Keycloak puts the user's GUID here. Entra uses a per-app pairwise pseudonym here, so it is not the right claim for cross-app user identity in Entra. | no |
| `jti` | **JWT ID.** Unique token identifier. Useful for blocklists and replay detection. | no |

`iat`, `nbf`, and `exp` are all Unix timestamps (seconds since 1970-01-01 UTC). They can be converted with `DateTimeOffset.FromUnixTimeSeconds(...)`.

---
### Entra-Specific Claims

A token issued by Microsoft Entra External ID carries the RFC claims above plus several Microsoft extensions. Understanding each one matters because the backend has to know which claim to read for which decision.

```json
{
  "typ": "JWT",
  "alg": "RS256",
  "kid": "k9xmStQ8T9T4sE9pnCn_-u4yUss"
}
.
{
  "aud": "b91fcda7-ce81-45c8-bd48-edcaf5d025f8",
  "iss": "https://fa7f808a-fe1d-4122-94b7-fd4ff4125521.ciamlogin.com/fa7f808a-fe1d-4122-94b7-fd4ff4125521/v2.0",
  "iat": 1779655234,
  "nbf": 1779655234,
  "exp": 1779659501,
  "aio": "AVQAq/8cAAAAGreQIWB9iWEGIU0AcfhcTmJGBDn7ISuAvgE74igsR8T5/...",
  "azp": "495ea5b7-8d00-4a09-b2de-37c67e3ee2c6",
  "azpacr": "0",
  "name": "Hamad",
  "oid": "c45493c2-bbee-4c65-9aad-4113762df1bf",
  "preferred_username": "hamad@gogameshop.com",
  "rh": "1.Ac8AioB_-h3-IkGUt_1P9BJVIafNH7mBzshFvUjtyvXQJfgAAMnPAA.",
  "roles": ["Admin"],
  "scp": "gogameshop_api.all",
  "sid": "004ffcaa-df1e-5b36-04d9-698c9dcd53d6",
  "sub": "7R5mTOi1w8UMoiKW25AWpmIKaWDX9wi6hcwx7AWum2Q",
  "tid": "fa7f808a-fe1d-4122-94b7-fd4ff4125521",
  "uti": "G5_CNOzc60SkrlJYSvUGAA",
  "ver": "2.0",
  "xms_ftd": "CR7w8ux36iiL8Pi-mvgOH_wnKmZA_870e7nviACjGPsBdXNzb3V0aC1kc21z"
}
.
<Signature>
```

What each claim means:

- **`aud`**: the application (client) ID of the API that registered itself in Entra. This is the GUID Entra assigned when you registered the backend. The API validates that the token's `aud` matches its own application ID, so a token meant for another API is rejected.
- **`iss`**: the issuer URL. For Entra External ID it has the form `https://<tenant-id>.ciamlogin.com/<tenant-id>/v2.0`. The tenant ID is embedded twice (subdomain and path). The trailing `/v2.0` signals the v2 token format. The API's `Authority` config must equal this string exactly.
- **`iat` / `nbf` / `exp`**: the three timestamps. `iat` is when Entra minted the token, `nbf` is the earliest time it is valid (usually equal to `iat`), and `exp` is the expiry (around 60 to 75 minutes later for Entra access tokens).
- **`aio`**: an internal Microsoft token, used by Entra for refresh and revocation. Opaque to the API; ignore it.
- **`azp`**: **authorized party.** The application ID of the client that requested this token, not the API. For example, the Postman client's app ID. Use this if you ever need to know "which app called me?", separate from "who is the user?".
- **`azpacr`**: how the authorized party authenticated to Entra. `"0"` means a public client (no secret); `"1"` means a confidential client that presented a secret; `"2"` means it used a certificate.
- **`name`**: human-readable display name of the user. For logging or UI, never for authorization.
- **`oid`**: **object ID.** The user's stable, unique identifier inside the Entra tenant, the same across every app in the tenant. This is the right claim for user identity in Entra, not `sub`.
- **`preferred_username`**: the user's sign-in name (typically email). Display only; users can change it.
- **`rh`**: refresh token hint. Opaque Microsoft internal value.
- **`roles`**: array of app role names the user has been assigned for this API. Note the name is **plural** (`roles`) where Keycloak uses singular (`role`). The JWT bearer scheme has to be told which claim name to treat as the role claim, hence the per-scheme `RoleClaimType` configuration.
- **`scp`**: space-separated list of OAuth scopes the client requested and was granted, for example `"gogameshop_api.all"`. Keycloak uses `scope` here; Entra uses `scp`. Both need to be split into individual claims for `RequireClaim` checks to match a single scope.
- **`sid`**: session ID inside Entra, used for single sign-out.
- **`sub`**: subject. For Entra, a per-application pairwise pseudonym, **not** a stable per-tenant user ID. Different apps see different `sub` values for the same user. Use `oid` for cross-app user identity instead.
- **`tid`**: **tenant ID.** Identifies which Entra tenant issued the token. Useful in multi-tenant scenarios where one API serves several tenants.
- **`uti`**: unique token identifier, similar to `jti`. Used by Entra for telemetry.
- **`ver`**: token format version. `2.0` means the v2 endpoint and v2 claim shape, which is what this project uses.
- **`xms_ftd`**: Microsoft-internal claim for telemetry and geo-routing. Ignore it.

---
### `dotnet user-jwts` Token vs Entra Token

`dotnet user-jwts` is a local development tool that mints JWTs signed with a key stored in the user's secret manager. It is invaluable for testing endpoints without standing up a real identity provider, but the token it produces looks meaningfully different from a real Entra token. Understanding the differences makes it easier to debug a token that "works in dev but fails in Entra mode" or vice versa.

A typical user-jwts payload looks like:

```json
{
  "unique_name": "hamad",
  "sub": "hamad",
  "jti": "1c4f...",
  "scope": ["gogameshop_api.all"],
  "role": ["Admin"],
  "iss": "dotnet-user-jwts",
  "aud": "http://localhost:5000",
  "nbf": 1779600000,
  "exp": 1779999999,
  "iat": 1779600000
}
```

Side-by-side:

| Aspect | `dotnet user-jwts` | Entra External ID |
|--------|--------------------|--------------------------|
| Issuer (`iss`) | Literal string `dotnet-user-jwts` | `https://<tenant>.ciamlogin.com/<tenant>/v2.0` |
| Audience (`aud`) | The API's localhost URL, e.g. `http://localhost:5000` | The API's application (client) ID GUID |
| Signing key | Symmetric key stored in user-secrets, local-only | RSA key pair; public keys published via JWKS |
| User identity | `sub` is the username you typed | `oid` is the user's tenant-wide GUID; `sub` is a per-app pseudonym |
| Username | `unique_name` | `preferred_username` |
| Role claim | `role` (singular), array of strings | `roles` (plural), array of strings |
| Scope claim | `scope` (singular), array (one claim per scope, already split) | `scp` (singular string, space-separated, needs splitting) |
| Extras | None | `azp`, `azpacr`, `oid`, `tid`, `sid`, `aio`, `rh`, `uti`, `ver`, etc. |
| Lifetime | Configurable, default very long for dev convenience | Around 60 to 75 minutes |
| Validation reach | Only this API knows the secret; no SSO | Any service trusting the same Entra tenant validates it |

Why the differences matter in code:
- The role and scope claim names are different per issuer. The JWT bearer scheme has to be told the right name for each, which is exactly why this project has one scheme per provider with its own `RoleClaimType`.
- The Entra `scp` claim is one space-separated string; the user-jwts and Keycloak `scope` claim already arrives as one claim per scope. The Entra path needs a claims transformer to split it; the user-jwts path does not.
- `dotnet user-jwts` is a development crutch. Production must not accept it; in this project the unnamed default scheme that trusts user-jwts is only registered when `builder.Environment.IsDevelopment()`.

The portal-side setup that produces these claims (tenant, app registration, scopes, client registration, consent, users, app roles) is covered separately in [notes/entra.md](entra.md). What follows here is only the JWT-validation side: how those claims get verified once a token arrives at the API.

---
### Configuring the API to Validate Entra Tokens

The API does not need to know any secret to validate Entra tokens. It only needs the issuer URL (to download the public keys) and the audience (to make sure the token was meant for this API).

**In `appsettings.json` under `Authentication:Schemes:Entra`:**
```json
"Entra": {
  "ValidAudience": "b91fcda7-ce81-45c8-bd48-edcaf5d025f8",
  "Authority": "https://fa7f808a-fe1d-4122-94b7-fd4ff4125521.ciamlogin.com/fa7f808a-fe1d-4122-94b7-fd4ff4125521/v2.0"
}
```

- **`Authority`** is the issuer URL. It must equal the token's `iss` claim exactly. From this URL the framework appends `/.well-known/openid-configuration` and fetches the OIDC discovery document, which lists the JWKS endpoint and the algorithms in use. To find this URL in the portal, open the application registration and look at "Endpoints"; the OpenID Connect metadata document URL contains the issuer. Alternatively, send a `GET` to the metadata URL and read the `issuer` field in the JSON response: that is exactly what `Authority` should be.
- **`ValidAudience`** is the API's own application (client) ID. It must equal the token's `aud` claim. (For Entra v2 tokens, the `aud` is the application's client ID GUID, not the tenant ID, even though both are GUIDs.)

**In code (`AuthorizationExtensions.cs`):**
```csharp
authBuilder.AddJwtBearer(
    Schemes.Entra,
    options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters.RoleClaimType = GoGameShopClaimTypes.Roles;
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                var transformer = context.HttpContext.RequestServices
                    .GetRequiredService<EntraClaimsTransformer>();
                transformer.Transform(context);
                return Task.CompletedTask;
            }
        };
    }
);
```

- `MapInboundClaims = false` keeps claim names as Entra sends them (`oid`, `scp`, `roles`, etc.) rather than remapping them to long Microsoft-style URLs.
- `RoleClaimType = GoGameShopClaimTypes.Roles` (note the plural) tells `RequireRole` and `User.IsInRole` to look at the `roles` claim instead of the default. This is the line that lets the same `RequireRole(Roles.Admin)` policy work for both Keycloak (`role`) and Entra (`roles`), once the right scheme is selected.
- `OnTokenValidated` invokes the `EntraClaimsTransformer`, which splits the `scp` space-separated string into individual `scope` claims and maps `oid` to a project-internal `userId` claim. After this runs, the same `RequireClaim(scope, "gogameshop_api.all")` and `FindFirstValue(userId)` checks work uniformly for tokens from either provider.

`Authority` and `ValidAudience` are picked up automatically by the JWT bearer middleware from `Authentication:Schemes:Entra` because of the configuration-binding convention introduced in .NET 8: as long as the section name matches the scheme name, no explicit `Bind(...)` is needed.

---
### Putting It All Together: One Request, End to End

A `POST /games` from Postman against the API running with Entra configured:

1. **Token acquisition.** Postman holds an OAuth 2.0 configuration pointing at the Entra tenant's authorization and token endpoints (discovered from the OIDC metadata document). It asks Entra for a token with scope `gogameshop_api.all`. Entra authenticates the user, checks that the Postman client has been granted that scope on the API (via admin consent), and returns a signed JWT whose `aud` is the API's application ID, `azp` is Postman's application ID, `scp` is `gogameshop_api.all`, and `roles` contains `Admin`.
2. **Request.** Postman sends the request with `Authorization: Bearer <token>`.
3. **Scheme selection.** The `KeycloakOrEntra` policy scheme reads the token, looks at `iss`, sees it contains `ciamlogin.com`, and forwards the request to the `Entra` scheme.
4. **Validation.** The Entra JWT bearer handler downloads (or uses cached) public keys from the issuer's JWKS endpoint, verifies the signature, checks `iss` matches `Authority`, `aud` matches `ValidAudience`, and that the current time is between `nbf` and `exp`.
5. **Claims transformation.** `OnTokenValidated` fires once. `EntraClaimsTransformer` splits `scp` into per-scope `scope` claims and copies `oid` to `userId`.
6. **Authorization.** The endpoint is registered with `RequireAuthorization(Policies.AdminAccess)`. The policy checks for `scope = gogameshop_api.all` (now present as a single claim after transformation) and the `Admin` role (resolved via the configured `roles` claim type). Both pass.
7. **Handler runs.** The request proceeds and the new game is created.

If any of those steps fail: missing scope returns `403`; expired token returns `401`; wrong `aud` returns `401`; missing role returns `403`. The HTTP status alone narrows down which check rejected the call.
