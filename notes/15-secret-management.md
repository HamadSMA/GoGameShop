## Secret Management

### Introduction

**The problem:**
Almost every non-trivial application needs values that must not be made public: a database password, a JWT signing key, a third-party API key, a connection string with credentials baked in. The naive places to put them are also the worst: hardcoded in source (leaks the moment the repo is shared), committed to `appsettings.json` (same problem, with the bonus of being copy-pasted into bug reports), or pasted into a wiki page (sprayed across whoever has access to the wiki). Once a secret is in git history, it is effectively public forever even if removed in a later commit.

**What it does:**
ASP.NET Core does not store secrets itself. What it does is treat secrets as just another **configuration source**: a layered, ordered set of providers that contribute key/value pairs into a single `IConfiguration`. Different providers are appropriate for different environments (developer laptop, CI build, Azure-hosted production), and the framework merges them so the application code reads them the same way every time. Picking the right provider for each environment is what "secret management" actually means in practice.

The mechanisms covered in this file, in roughly increasing level of operational maturity:

1. **User Secrets** : developer laptop, plaintext outside the repo
2. **Environment variables** : per-process, works everywhere
3. **Command-line arguments** : one-off overrides
4. **Container and Kubernetes secrets** : files or env vars injected by the orchestrator
5. **Azure Key Vault** : managed cloud secret store
6. **Azure App Configuration** : centralized config with Key Vault references
7. **Managed Identity** : the long-term goal of removing secrets entirely by authenticating as the resource itself

What should **never** hold a secret: `appsettings.json`, `appsettings.{Environment}.json`, anything checked into source control, anything in a Docker image layer, anything in a log line.

---
### The Configuration System and Providers

**The problem:**
Every mechanism below needs to feed values into the same `IConfiguration` that the rest of the application reads from. If each one had its own bespoke API, the application code would be a mess of `if (env == "Dev") userSecrets.Get(...) else if (env == "Azure") keyVault.Get(...)`. The whole point of unified configuration is that the application never knows or cares where a value came from.

**What it does:**
`IConfiguration` is a layered key/value store built from an ordered list of **configuration providers**. Each provider knows how to read from one source (a JSON file, environment variables, a Key Vault, a database). When the application asks for `Configuration["ConnectionStrings:GoGameShop"]`, the configuration system asks each provider in reverse order : the last provider added wins.

`WebApplication.CreateBuilder(args)` automatically wires up these providers in this order:

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. User Secrets (only when `ASPNETCORE_ENVIRONMENT=Development`)
4. Environment variables
5. Command-line arguments (`args`)

Additional providers (Key Vault, App Configuration, custom ones) are added by calling `builder.Configuration.Add...` before `builder.Build()`.

**Key syntax for nested values:**
Configuration keys use colons as separators in code (`ConnectionStrings:GoGameShop`) regardless of the source. Each provider translates from its native syntax:

| Source              | How to express `ConnectionStrings:GoGameShop`   |
|---------------------|-------------------------------------------------|
| JSON file           | Nested object: `"ConnectionStrings": { "GoGameShop": "..." }` |
| User Secrets        | Same JSON format                                |
| Environment variable | `ConnectionStrings__GoGameShop` (double underscore for nesting, since most shells choke on colons) |
| Command-line arg    | `--ConnectionStrings:GoGameShop=...`            |
| Key Vault           | Secret named `ConnectionStrings--GoGameShop` (double dash) |

The application always reads `Configuration["ConnectionStrings:GoGameShop"]`; the translation is the provider's job.

---
### User Secrets

**The problem:**
On a developer laptop, the application needs a real database password and a real client secret to talk to a local Keycloak. Putting these in `appsettings.Development.json` means they get committed. Setting environment variables works but is annoying: every developer has to remember to set them in their shell or IDE, and switching projects means juggling overlapping variable names. A per-project, per-developer store that lives **outside the repo** is the cleanest fit.

**What it does:**
**User Secrets** is a development-only configuration source that stores values **outside the project directory**, in the current user's profile. The file lives at:

- macOS / Linux: `~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`
- Windows: `%APPDATA%\Microsoft\UserSecrets\<UserSecretsId>\secrets.json`

The `<UserSecretsId>` is a GUID stored in the `.csproj`; it is the link between a project and its secrets folder. Because the secrets file lives outside the repo, it cannot be committed by accident. Because it is keyed per project, switching repos automatically switches secret sets.

User Secrets are **only loaded when `ASPNETCORE_ENVIRONMENT=Development`**, so production never sees them. They are also **plaintext on disk**: this is not encryption, just "keep secrets out of git." For real production secrets use Key Vault, not User Secrets.

**Enabling on a project:**
```
dotnet user-secrets init
```

This adds a `<UserSecretsId>` to the `.csproj`:
```xml
<PropertyGroup>
  <UserSecretsId>5a5a7b30-a362-4663-8e75-e95323484565</UserSecretsId>
</PropertyGroup>
```

No `Program.cs` changes are needed: `WebApplication.CreateBuilder` calls `AddUserSecrets()` automatically when the environment is Development and the assembly has a `UserSecretsId` attribute.

**Setting, reading, listing, removing:**
```
dotnet user-secrets set "ConnectionStrings:GoGameShop" "Server=...;Password=hunter2"
dotnet user-secrets set "Keycloak:ClientSecret" "abc123"
dotnet user-secrets list
dotnet user-secrets remove "Keycloak:ClientSecret"
dotnet user-secrets clear
```

Colons in the key map to nested JSON, exactly as with `appsettings.json`.

**In this project:**
`Backend/src/GoGameShop.Api/GoGameShop.Api.csproj` has a registered `UserSecretsId`. Local development secrets (Keycloak client secret, any future DB password) go here rather than into `appsettings.Development.json`, so the committed config files keep only non-sensitive defaults.

---
### Environment Variables

**The problem:**
User Secrets is great on a laptop but does not exist on a CI runner, a Docker container, or an App Service instance, where there is no "user profile" to read from. The lowest-common-denominator way to inject configuration into a process on every OS is the one that has existed since the 1970s: environment variables.

**What it does:**
ASP.NET Core's environment variable provider reads variables from the process environment and exposes them through `IConfiguration`. Two conventions matter:

- **Nesting** : use **double underscore** `__` instead of a colon. Most shells forbid colons in variable names, so `ConnectionStrings__GoGameShop` maps to the key `ConnectionStrings:GoGameShop`.
- **Prefix filtering** : `AddEnvironmentVariables(prefix: "MYAPP_")` will only read variables that start with `MYAPP_` and will strip the prefix. The default registration reads all variables, plus a dedicated provider that reads `ASPNETCORE_`-prefixed variables for framework settings.

A few special variables the framework checks before configuration is even built:
- `ASPNETCORE_ENVIRONMENT` : `Development`, `Staging`, or `Production` (controls which `appsettings.{Env}.json` is loaded and whether User Secrets are read)
- `ASPNETCORE_URLS` : the URLs Kestrel binds to (e.g. `http://+:8080`)
- `DOTNET_RUNNING_IN_CONTAINER=true` : set by Microsoft base container images so the runtime knows it is containerized

**In code (generic):**
```
# bash
export ConnectionStrings__GoGameShop="Server=...;Password=hunter2"
dotnet run
```

```
# Docker
docker run -e ConnectionStrings__GoGameShop="Server=...;Password=hunter2" myapp
```

```yaml
# docker-compose.yml
services:
  api:
    environment:
      - ConnectionStrings__GoGameShop=Server=...;Password=hunter2
```

**Where this is the right tool:**
- Docker Compose for local infrastructure (Keycloak, databases): values that are not secret-secret but vary per environment
- CI/CD pipelines injecting build-time configuration
- App Service / Container Apps "application settings" (which are surfaced to the app as environment variables)

**Where it is not:**
- Long-lived secrets in production. Environment variables on an App Service are stored by Azure but are visible to anyone with `Contributor` on the resource and show up in `az webapp config appsettings list`. A real secret belongs in Key Vault, with the App Service only holding a Key Vault reference (covered below).

---
### Command-line Arguments

**The problem:**
Sometimes you want to override one config value for a single run : try a different connection string, point at a staging Key Vault, flip a feature flag : without touching files or shell variables. Editing `appsettings.json` for a one-off invocation is overkill; setting an env var pollutes the shell.

**What it does:**
`WebApplication.CreateBuilder(args)` registers a command-line configuration provider that reads `args` and turns `--Key=Value` (or `--Key Value`, or `/Key=Value`) into configuration entries. Command-line args are added **last**, so they override every other source : exactly what is wanted for a temporary override.

**In code (generic):**
```
dotnet run -- --ConnectionStrings:GoGameShop="Server=...;Password=hunter2" --Logging:LogLevel:Default=Debug
```

The `--` separates `dotnet run`'s own arguments from arguments passed to the app.

**Why this is rarely used for actual secrets:**
On most operating systems, the command line of a running process is visible to anyone with `ps`. Secrets handed in via `--` end up in shell history and process listings. Useful for "I want to try this connection string just this once," not for production credentials.

---
### Container and Kubernetes Secrets

**The problem:**
Containers add their own challenge: anything baked into the image (env values in the Dockerfile, files in a `COPY` layer) is permanently part of that image and can be extracted by anyone who pulls it. Secrets have to arrive **at run time**, not at build time, and the runtime (Docker, Kubernetes) needs to be the one putting them there.

**What it does:**
Two orchestrator-level mechanisms, both of which surface to the application as ordinary configuration sources:

**Docker secrets (Swarm) / bind mounts:**
A secret is stored by the Docker daemon and mounted into the container at `/run/secrets/<name>` as a read-only file. ASP.NET Core can read it with `builder.Configuration.AddKeyPerFile("/run/secrets", optional: true)`, which treats each file in the directory as a configuration key (file name) and value (file contents).

**Kubernetes Secrets:**
A `Secret` resource holds key/value pairs and can be projected into a pod two ways:
- As **environment variables** via `env.valueFrom.secretKeyRef` (ASP.NET Core's env-var provider picks them up)
- As **files** mounted into a directory via `volumes.secret` (ASP.NET Core's `AddKeyPerFile` picks them up)

```yaml
# Kubernetes pod (excerpt)
env:
  - name: ConnectionStrings__GoGameShop
    valueFrom:
      secretKeyRef:
        name: gogameshop-db
        key: connection-string
```

K8s secrets are **base64-encoded, not encrypted** by default. Encryption-at-rest in etcd has to be enabled separately. For real secret storage in Kubernetes, most teams point K8s at an external store (Key Vault, Vault, AWS Secrets Manager) via a CSI driver.

---
### Azure Key Vault

**The problem:**
On a developer laptop User Secrets is enough; in production something more robust is needed. Secrets should be:
- Stored encrypted at rest, in a system designed for it
- Auditable: who read which secret, when
- Rotatable: a new version of a secret can be issued without touching the application
- Access-controlled: only specific identities (and not, say, every contributor on the subscription) can read them

**What it does:**
**Azure Key Vault** is a managed secret store. It holds three kinds of objects:
- **Secrets** : arbitrary strings (connection strings, API keys, passwords)
- **Keys** : cryptographic keys for signing or encryption, usable without ever leaving the vault
- **Certificates** : TLS certs with rotation built in

Each secret has versions, an enabled/disabled flag, optional expiration, and access logs. RBAC controls who and what can read it; the relevant data-plane role is `Key Vault Secrets User` (read) or `Key Vault Secrets Officer` (read/write).

The configuration package `Azure.Extensions.AspNetCore.Configuration.Secrets` adds a Key Vault provider:

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://gogameshop-kv.vault.azure.net/"),
    new DefaultAzureCredential());
```

At startup the provider pulls every secret in the vault and exposes them as configuration entries. The vault's naming convention is that **`--` (double dash)** in a secret name becomes `:` in the configuration key : so a secret named `ConnectionStrings--GoGameShop` shows up as `Configuration["ConnectionStrings:GoGameShop"]`. Periods are not allowed in vault names, hence the convention.

**Reloading:**
Secrets are pulled at startup. To pick up new versions without a restart, pass a reload interval:

```csharp
builder.Configuration.AddAzureKeyVault(
    vaultUri,
    new DefaultAzureCredential(),
    new AzureKeyVaultConfigurationOptions { ReloadInterval = TimeSpan.FromMinutes(10) });
```

**How the app authenticates to the vault:**
The next section. The whole point is that the app does **not** carry a secret to read its secrets : that would just shift the problem.

---
### Azure App Configuration

**The problem:**
Key Vault is for secrets, but most "configuration" is not secret : feature flags, log levels, region settings, retry counts. Putting all of that into Key Vault works but is awkward (Key Vault has per-operation cost, no native feature-flag UI, no environment labels). A separate service for non-secret config that **references** Key Vault for the secret parts is a cleaner split.

**What it does:**
**Azure App Configuration** is a centralized key/value store with:
- **Labels** : the same key can have different values per environment (label `Production`, `Staging`, `Dev`)
- **Feature flags** : first-class on/off toggles with a UI
- **Key Vault references** : a key whose value is a pointer to a Key Vault secret; the App Configuration client resolves it transparently

```csharp
builder.Configuration.AddAzureAppConfiguration(options =>
{
    options.Connect(new Uri("https://gogameshop-appcfg.azconfig.io"), new DefaultAzureCredential())
           .Select(KeyFilter.Any, LabelFilter.Null)
           .Select(KeyFilter.Any, builder.Environment.EnvironmentName)
           .ConfigureKeyVault(kv => kv.SetCredential(new DefaultAzureCredential()));
});
```

This is essentially "appsettings.json in the cloud, with feature flags and Key Vault integration." For a small project it is overkill: App Service + Key Vault directly works fine. For a multi-service deployment with shared feature flags it earns its keep.

---
### Managed Identity

**The problem:**
Every mechanism above eventually hits the same chicken-and-egg problem: the application needs **some** credential to authenticate to the secret store. A connection string for Key Vault is just another secret. A client ID and client secret for a service principal is just another secret. The only real way out is to make the secret store trust the application's identity directly, without any credential the application has to hold.

**What it does:**
A **Managed Identity** is an identity that Azure assigns to an Azure resource (App Service, Container App, Function, VM, AKS pod via workload identity). The resource can request a token for itself from the Azure Instance Metadata Service at runtime; nothing about that identity is configured inside the application. Grant that identity `Key Vault Secrets User` on a vault, and the application can read secrets with **zero credentials in code or config**.

Two flavors:
- **System-assigned** : tied to the lifecycle of the resource; deleted when the resource is deleted. One per resource. Best default.
- **User-assigned** : a standalone identity that can be attached to multiple resources. Useful when several App Services need the same permissions.

**How the application uses it:**
`DefaultAzureCredential` from the `Azure.Identity` package walks an ordered list of credential sources and uses the first one that works:

1. Environment variables (a service principal, if set)
2. Workload identity (Kubernetes federation)
3. Managed identity (when running on Azure)
4. Azure CLI (`az login`) : matches the developer logged into `az` locally
5. Visual Studio / Rider sign-in
6. Interactive browser

This is the magic that makes "the same code works on my laptop and in production." Locally, `az login` provides the credential; in Azure, the platform's managed identity does. No code change between environments.

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://gogameshop-kv.vault.azure.net/"),
    new DefaultAzureCredential());
```

The same line works in dev (against the developer's `az login` identity, which must be granted `Key Vault Secrets User` on a dev vault) and in prod (against the App Service's managed identity).

**In this project:**
Phase 5 will set up a managed identity on the App Service, grant it `Key Vault Secrets User` on a project Key Vault, store the production database password and Keycloak client secret there, and have `DefaultAzureCredential` resolve to the App Service's identity. Locally, the same code will resolve to `az login`, against a dev-only vault.

---
### Configuration Source Ordering

**The problem:**
With this many sources potentially holding the same key, it has to be predictable which one wins. Surprises here turn into "why does the production app keep connecting to the dev database" incidents.

**What it does:**
The default order set up by `WebApplication.CreateBuilder` is, from lowest to highest precedence (later wins):

1. `appsettings.json`
2. `appsettings.{Environment}.json`
3. **User Secrets** (Development only)
4. Environment variables
5. Command-line arguments

Anything added with `builder.Configuration.Add...` after `CreateBuilder` returns is appended to that list and therefore wins over everything above it. A typical layout for an Azure-hosted app:

1. `appsettings.json` (committed defaults: log levels, non-secret URLs)
2. `appsettings.Production.json` (committed prod overrides: non-secret)
3. (No User Secrets in production)
4. Environment variables (App Service application settings: still mostly non-secret)
5. Command-line arguments (rare)
6. **Azure Key Vault** (added explicitly: production secrets win over everything else)

For local dev:

1. `appsettings.json`
2. `appsettings.Development.json`
3. **User Secrets** (local secrets)
4. Environment variables (Docker Compose values for `localinfra`)
5. Command-line arguments

A useful rule of thumb: **the less sensitive a value is, the lower in the stack it should live.** Committed defaults at the bottom, environment-specific config above them, secrets at the top : added by a provider that is itself secured by identity (Managed Identity into Key Vault), not by another secret.
