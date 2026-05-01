## Project Setup and Configuration

### ASP.NET Core & Minimal APIs

**The problem:**
Traditional ASP.NET MVC requires a controller class for every group of endpoints, with attribute-routed methods, model binders, and a fair bit of boilerplate per route. For a small API, that's a lot of ceremony for what amounts to "respond to GET /games."

**What it does:**
ASP.NET Core is Microsoft's framework for building web apps and APIs with C#. A **Minimal API** is a lightweight way to define HTTP endpoints directly in code — `app.MapGet("/games", handler)` with no controller class, no attributes, no inherited base. The result is less code per endpoint and a faster path from "I need a route" to a running route.

**In code:**
Every endpoint in this project (`MapGet`, `MapPost`, etc.) is a Minimal API. Instead of a `GamesController` class, there is a `MapGames()` method that registers all game-related routes.

---
### Program.cs — The Entry Point

**The problem:**
A web app has dozens of moving pieces — database, logging, routing, middleware, configuration, validation — that all need to be wired together in a specific order before the first request arrives. Spreading that wiring across many files makes it impossible to reason about what's actually registered or what runs first.

**What it does:**
`Program.cs` is the single entry point where everything gets configured and the app starts. It splits cleanly into two phases — *builder* (register services into the DI container) and *app* (configure the pipeline and run) — so the lifecycle is obvious from top to bottom.

**In code:**
```csharp
var builder = WebApplication.CreateBuilder(args);  // Creates the app builder
var connectionString = builder.Configuration.GetConnectionString("GoGameShop");

builder.Services.AddOpenApi();                                    // Register OpenAPI doc generation
builder.Services.AddSqlite<GoGameShopContext>(connectionString);  // Register the database
builder.Services.AddValidation();                                 // Register validation
builder.Services.AddHttpLogging(options =>                        // Configure HTTP logging
{
    options.LoggingFields = HttpLoggingFields.RequestMethod |
                            HttpLoggingFields.RequestPath |
                            HttpLoggingFields.ResponseStatusCode |
                            HttpLoggingFields.Duration;
    options.CombineLogs = true;
});

var app = builder.Build();                                        // Build the app

app.MapGames();                     // Register /games endpoints
app.MapGetGenres();                 // Register /genres endpoint
app.MapGetRatings();                // Register /ratings endpoint

app.UseHttpLogging();               // Log each request/response

if (app.Environment.IsDevelopment())
    app.MapOpenApi();               // Expose OpenAPI spec at /openapi/v1.json (dev only)
else
    app.UseExceptionHandler();      // Handle unhandled exceptions in production

app.UseStatusCodePages();           // Add Problem Details bodies to error responses
await app.InitializeDbAsync();      // Run migrations & seed data (async)

app.Run();               // Start the server
```

The two phases are:
- **Builder phase** (`builder.Services.*`): Register services into the DI container
- **App phase** (`app.*`): Configure the pipeline and run

---
### WebApplication & WebApplicationBuilder

**The problem:**
Setting up a web app is naturally two-phased: first declare what exists (services, config sources, logging providers), then use those declarations to wire up the pipeline and start handling requests. If both phases share one object, it's easy to accidentally consume a service before it's fully registered, or to mutate registrations after they've been baked in.

**What they are:**
`WebApplicationBuilder` (returned by `WebApplication.CreateBuilder(args)`) is for the *registration* phase. Calling `builder.Build()` locks the registrations and returns a `WebApplication` — the running app, used for the *usage* phase (mapping routes, adding middleware, calling `Run()`).

```csharp
var builder = WebApplication.CreateBuilder(args);  // WebApplicationBuilder
// ... register services on builder ...
var app = builder.Build();                         // WebApplication
// ... map routes on app ...
app.Run();
```

**What `WebApplication.CreateBuilder()` preconfigures for you:**
Calling `CreateBuilder()` is not a blank slate — it sets up a large number of defaults so you don't have to:

| Default | What it does |
|---------|-------------|
| **Kestrel HTTP server** | The built-in web server that listens for HTTP requests. No IIS or external server needed. |
| **Configuration system** | Loads config in priority order: `appsettings.json` → `appsettings.{Environment}.json` → environment variables → command-line args. Later sources override earlier ones. |
| **Logging** | Sets up logging to the Console and Debug output, with log levels read from `appsettings.json`. |
| **DI container** | Initializes `builder.Services` (an `IServiceCollection`) — the registry for all your app's dependencies. |
| **Environment detection** | Reads the `ASPNETCORE_ENVIRONMENT` variable and exposes it as `builder.Environment`. Drives which appsettings file is loaded and whether developer tools are on. |
| **Content root** | Sets the working directory for the app (where it looks for files like `appsettings.json`). Defaults to the directory the app runs from. |

**Accessing these defaults:**

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Configuration   // Read config values
builder.Services        // Register services into DI
builder.Logging         // Configure logging providers
builder.Environment     // Check IsDevelopment(), IsProduction(), etc.
builder.Host            // Configure the underlying host
builder.WebHost         // Configure Kestrel / web server settings
```

**After `Build()`**, the `WebApplication` object (`app`) doubles as both the app and the middleware pipeline:

```csharp
var app = builder.Build();

app.Services            // Resolve services from the DI container
app.Configuration       // Access config (same as builder.Configuration)
app.Environment         // Access environment info
app.MapGet(...)         // Register routes
app.Use(...)            // Add middleware
app.Run()               // Start listening for requests
```

---
### Key ASP.NET Core Classes and Interfaces

A quick reference for the most important types in ASP.NET Core and the methods you'll call most often.

---

**`WebApplicationBuilder`** — configures everything before the app starts

| Member | What it does |
|--------|-------------|
| `builder.Services` | `IServiceCollection` — register services into DI |
| `builder.Configuration` | `ConfigurationManager` — read config values |
| `builder.Logging` | `ILoggingBuilder` — configure logging providers |
| `builder.Environment` | `IWebHostEnvironment` — check current environment |
| `builder.Build()` | Locks in all registrations and returns the `WebApplication` |

---

**`WebApplication`** — the running app; doubles as the middleware pipeline and route builder

| Member | What it does |
|--------|-------------|
| `app.MapGet/Post/Put/Delete(path, handler)` | Register a route endpoint |
| `app.MapGroup(prefix)` | Create a route group with a shared URL prefix |
| `app.Use(middleware)` | Add inline middleware |
| `app.UseMiddleware<T>()` | Add a class-based middleware |
| `app.UseRouting()` | Enable endpoint routing |
| `app.UseAuthentication()` | Identify who the user is |
| `app.UseAuthorization()` | Check if they're allowed |
| `app.UseHttpLogging()` | Log each request and response |
| `app.Run()` / `app.RunAsync()` | Start the server |
| `app.Services` | Resolve services from the DI container |
| `app.Logger` | Built-in `ILogger` scoped to the app |
| `app.Configuration` | Access config (same data as `builder.Configuration`) |
| `app.Environment` | Access environment info |

---

**`HttpContext`** — represents the full HTTP transaction for one request; passed through the middleware pipeline

| Member | What it does |
|--------|-------------|
| `context.Request.Method` | HTTP verb: `GET`, `POST`, etc. |
| `context.Request.Path` | Request path: `/games/123` |
| `context.Request.Query["key"]` | Query string value |
| `context.Request.Headers["key"]` | Request header value |
| `context.Response.StatusCode` | Set or read the response status code |
| `context.Response.Headers` | Set response headers |
| `context.User` | The authenticated user (`ClaimsPrincipal`) |
| `context.Items` | Key/value store for sharing data between middleware in one request |
| `context.RequestAborted` | `CancellationToken` that fires if the client disconnects |

---

**`IServiceCollection`** — the DI registration registry; available as `builder.Services`

| Method | What it does |
|--------|-------------|
| `AddSingleton<T>()` | One shared instance for the entire app lifetime |
| `AddScoped<T>()` | One instance per HTTP request |
| `AddTransient<T>()` | New instance every time it's resolved |
| `AddSqlite<TContext>()` | Register an EF Core `DbContext` with SQLite |
| `AddHttpLogging()` | Register and configure HTTP logging middleware |
| `AddValidation()` | Enable automatic model validation on endpoints |
| `Configure<TOptions>()` | Bind a config section to a typed options class |

---

**`ILogger<T>`** — structured logging; injected via DI or accessed as `app.Logger`

| Method | When to use |
|--------|------------|
| `LogTrace()` | Extremely detailed; disabled by default |
| `LogDebug()` | Diagnostic info for development |
| `LogInformation()` | Normal operational events ("Game created") |
| `LogWarning()` | Unexpected but non-fatal |
| `LogError()` | Failures that need attention |
| `LogCritical()` | App-breaking failures |
| `IsEnabled(LogLevel)` | Check if a level is active before building an expensive message |

---

**`IConfiguration`** — reads config from `appsettings.json`, environment variables, and other sources

| Method | What it does |
|--------|-------------|
| `GetValue<T>("key")` | Read a single typed value |
| `GetSection("key")` | Get a subsection as `IConfigurationSection` |
| `GetConnectionString("name")` | Shorthand for the `ConnectionStrings` section |
| `["key"]` indexer | Read a raw string value |

---

**`IWebHostEnvironment`** — available as `app.Environment` or `builder.Environment`

| Member | What it does |
|--------|-------------|
| `IsDevelopment()` | True when `ASPNETCORE_ENVIRONMENT` is `"Development"` |
| `IsProduction()` | True in production |
| `IsStaging()` | True in staging |
| `EnvironmentName` | The raw environment name string |

---

**`IEndpointRouteBuilder`** — the interface behind `WebApplication` and route groups for registering endpoints; used as the parameter type in extension methods like `MapGames(this IEndpointRouteBuilder app)`

| Method | What it does |
|--------|-------------|
| `MapGet(pattern, handler)` | Register a GET endpoint |
| `MapPost(pattern, handler)` | Register a POST endpoint |
| `MapPut(pattern, handler)` | Register a PUT endpoint |
| `MapDelete(pattern, handler)` | Register a DELETE endpoint |
| `MapGroup(prefix)` | Create a sub-group with a shared URL prefix |

---
### The .csproj File — Project Configuration

**The problem:**
A project needs to declare which .NET version it targets, which NuGet packages it depends on, and which compiler options apply — and that declaration needs to be deterministic, version-controllable, and machine-readable for the build, restore, and IDE all to agree.

**What it does:**
The `.csproj` file is an XML manifest for the project: target framework, package references, language settings, build options. The .NET SDK and every IDE read this same file, so the project's identity is defined in one place and is the same everywhere it's built.

**In code:**
```xml
<TargetFramework>net10.0</TargetFramework>   <!-- Use .NET 10 -->
<Nullable>enable</Nullable>                  <!-- Enable nullable reference types -->
<ImplicitUsings>enable</ImplicitUsings>      <!-- Auto-include common namespaces -->

<PackageReference Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.5" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.5" />
```

- `Nullable>enable` means the compiler warns you if you might accidentally use a null value — a common source of crashes.
- `ImplicitUsings>enable` means common namespaces like `System` are included automatically.
- The `PackageReference` entries pull in EF Core and the SQLite driver from NuGet.

---
### appsettings.json — App Configuration

**The problem:**
Values like connection strings, log levels, and feature flags differ between environments and change over time. Hardcoding them means recompiling for every tweak; spreading them across env vars alone makes the configuration shape invisible to the code that reads it.

**What it does:**
`appsettings.json` is the file-based source for application configuration. ASP.NET Core layers it with `appsettings.{Environment}.json` and environment variables (later sources override earlier ones), so the same code reads the right value in dev, staging, and production without changes.

**In code:**
```json
"ConnectionStrings": {
  "GoGameShop": "Data Source=GoGameShop.db"
}
```

In `Program.cs`, this is read with:
```csharp
builder.Configuration.GetConnectionString("GoGameShop")
```

This tells EF Core to use a SQLite database file named `GoGameShop.db` in the project directory.

`appsettings.Development.json` overrides settings for the Development environment. Later overrides win — so the same key in `appsettings.Development.json` replaces the value from `appsettings.json`.

**Current config layout in this project:**

`appsettings.json` (base — applies to all environments):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
      "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware": "Information"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "GoGameShop": "Data Source=GoGameShop.db"
  }
}
```

`appsettings.Development.json` (dev overrides):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware": "None"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "GoGameShop": "Data Source=GoGameShop.db"
  }
}
```

The key difference: HTTP logging is `Information` in production (base config) but `None` in development — because in dev you don't need per-request HTTP logs cluttering the console, but you do want them in production for observability.

---
### Logging

**The problem:**
`Console.WriteLine` is unstructured, unfilterable, and tied to one destination. As soon as the app needs to ship logs to a file, an aggregator, or a cloud service — or to silence noisy categories without recompiling — print-style logging falls apart.

**What it does:**
ASP.NET Core's built-in `ILogger<T>` is a structured logger registered automatically by `WebApplicationBuilder`. Calls capture severity (`Information`, `Warning`, `Error`, …) and named properties — not just text — so log aggregators can filter and query, and a swap to Serilog or NLog later doesn't change the call sites.

**In code — injecting into an endpoint:**
```csharp
app.MapPost("/", async (
    GoGameShopContext dbContext,
    CreateGameDto gameDto,
    ILogger<Program> logger) =>       // Injected just like DbContext
{
    // ... create game ...

    logger.LogInformation("Created Game {GameName} with price {GamePrice}",
        game.Name,
        game.Price);
});
```

The `{GameName}` and `{GamePrice}` placeholders are **structured logging** — the logger stores `GameName` and `GamePrice` as named properties, not just as text. This means log aggregation tools (like Application Insights or Seq) can filter, query, and group by these values.

**How it fits — logging from `WebApplication` directly:**
```csharp
public static async Task InitializeDbAsync(this WebApplication app)
{
    await app.MigrateDbAsync();
    await app.SeedDbAsync();
    app.Logger.LogInformation("Database initialized");  // app has a built-in logger
}
```

`WebApplication` exposes `app.Logger` directly — no injection needed in extension methods where you already have the `app` reference.

**Log levels (lowest → highest severity):**

| Level | Method | When to use |
|-------|--------|-------------|
| `Trace` | `LogTrace()` | Extremely detailed, usually disabled |
| `Debug` | `LogDebug()` | Diagnostic info for debugging |
| `Information` | `LogInformation()` | Normal operational events ("Game created") |
| `Warning` | `LogWarning()` | Something unexpected but recoverable |
| `Error` | `LogError()` | A failure that needs attention |
| `Critical` | `LogCritical()` | App-breaking failure |

**Filtering by log level in `appsettings.json`:**
```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning",
    "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
  }
}
```

Each key is a logger category (usually the namespace or class name). The value is the minimum level to show — anything below it is suppressed. `"Microsoft.EntityFrameworkCore.Database.Command": "Warning"` silences EF Core's SQL output, which would otherwise print every query.

**Built-in HTTP request logging (`AddHttpLogging`):**

ASP.NET Core ships a built-in HTTP logging middleware that logs each request and response. You configure what fields to capture in the builder phase and activate it in the pipeline:

```csharp
// Program.cs — builder phase
builder.Services.AddHttpLogging(options =>
{
    options.LoggingFields = HttpLoggingFields.RequestMethod |
                            HttpLoggingFields.RequestPath |
                            HttpLoggingFields.ResponseStatusCode |
                            HttpLoggingFields.Duration;
    options.CombineLogs = true;  // One log entry per request instead of two
});

// Program.cs — pipeline phase
app.UseHttpLogging();
```

`HttpLoggingFields` is a flags enum — you OR together the fields you want. Common options:

| Field | What it logs |
|-------|-------------|
| `RequestMethod` | GET, POST, etc. |
| `RequestPath` | `/games/123` |
| `ResponseStatusCode` | 200, 404, etc. |
| `Duration` | How long the request took |
| `RequestHeaders` | Incoming headers |
| `ResponseHeaders` | Outgoing headers |
| `RequestBody` | Request payload (careful — can be large) |

The log category for HTTP logging is `Microsoft.AspNetCore.HttpLogging.HttpLoggingMiddleware`, which lets you control its verbosity independently in `appsettings.json`.

---
### Global Usings

**The problem:**
The same handful of namespaces (`System`, the project's `Models`, `Data`, `Features`) get imported at the top of nearly every file. Each `using` line is small, but multiplied across the project they add up to noise and merge friction without communicating anything specific to the file.

**What it does:**
A `global using` declaration in one file (`GlobalUsings.cs`) applies the import across every file in the project. Project-wide namespaces get declared once and disappear from individual files, leaving only the per-file imports that actually convey something.

**In code:**
```csharp
global using GoGameShop.Api.Models;
global using GoGameShop.Api.Data;
global using GoGameShop.Api.Features.Games;
// ...etc
```

Now any file in the project can reference `Game`, `GoGameShopContext`, or `GetGamesEndpoint` without importing anything.

---
### Middleware

**The problem:**
Cross-cutting concerns — logging, auth, exception handling, compression, CORS — apply to every request, but baking them into each endpoint duplicates code and inevitably misses cases. There needs to be a way to layer behavior around the endpoint instead of inside it.

**What it does:**
**Middleware** is code that sits in the HTTP request pipeline and processes every request and response that passes through. Each piece can inspect or modify the request on the way in, call the next middleware, then inspect or modify the response on the way out — letting cross-cutting concerns live outside the endpoint.

```
Incoming request
      ↓
 [Middleware A]  ← runs first on the way in, last on the way out
      ↓
 [Middleware B]
      ↓
 [Middleware C]
      ↓
   Endpoint
      ↓
 [Middleware C]  ← runs again on the way out
      ↑
 [Middleware B]
      ↑
 [Middleware A]  ← runs last on the way out
      ↑
Outgoing response
```

**Key types:**

`RequestDelegate` — the core delegate type of the pipeline. It's a function that takes an `HttpContext` and returns a `Task`. Every middleware is ultimately a `RequestDelegate`:
```csharp
// RequestDelegate is defined as:
public delegate Task RequestDelegate(HttpContext context);
```

`HttpContext` — represents the entire HTTP transaction. It holds:
- `context.Request` — the incoming request (method, path, headers, body, query string)
- `context.Response` — the outgoing response (status code, headers, body)
- `context.User` — the authenticated user (if any)
- `context.Items` — a dictionary for passing data between middleware in the same request

`IMiddleware` — an interface-based alternative to the convention approach. Requires implementing a single `InvokeAsync` method and must be registered in DI:
```csharp
public class MyMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // before
        await next(context);
        // after
    }
}
```

**Convention-based custom middleware (used in this project):**

The more common pattern — no interface required. The class must have a constructor that accepts `RequestDelegate next` and a public `InvokeAsync(HttpContext)` method:

```csharp
public class RequestTimingMiddleware(
    RequestDelegate next,
    ILogger<RequestTimingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);         // hand off to next middleware
        }
        finally
        {
            stopwatch.Stop();
            logger.LogInformation(
                "{Method} {Path} {StatusCode} in {Ms}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }
}
```

`try/finally` ensures the timing log fires even if the next middleware throws. The `finally` block always runs — on success, on exception, or on cancellation.

**Registration:**
```csharp
app.UseMiddleware<RequestTimingMiddleware>();
```

Additional constructor dependencies (like `ILogger`) are resolved from DI automatically — you only need to pass `RequestDelegate next` yourself; everything else is injected.

---
### Middleware Order

**The problem:**
Middleware runs in registration order on the way in and the reverse on the way out — and the order is load-bearing. Authorization before routing means the router hasn't run yet, so the auth check has no endpoint to inspect. Static files after auth means anonymous users get `401` for images. Both bugs are silent and easy to miss.

**What to know:**
The order in which you call `app.Use*()` in `Program.cs` *is* the pipeline order. There's no auto-sort; the framework runs them as you wrote them. The recommended ordering below isn't arbitrary — each item depends on something earlier having already run.

**Why order matters:**
```
// WRONG — auth runs before routing, so app.User.Identity is not set yet for the endpoint
app.UseAuthorization();
app.UseRouting();

// CORRECT
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
```

**Recommended order for a typical ASP.NET Core app:**

```csharp
app.UseExceptionHandler();       // 1. Catch all unhandled exceptions (outermost)
app.UseHttpsRedirection();       // 2. Redirect HTTP → HTTPS
app.UseStaticFiles();            // 3. Serve static files (CSS, JS, images) before routing
app.UseRouting();                // 4. Match request to an endpoint
app.UseCors();                   // 5. Apply CORS headers
app.UseAuthentication();         // 6. Identify who the user is
app.UseAuthorization();          // 7. Check if they're allowed
app.UseHttpLogging();            // 8. Log the request (after auth so user info is available)
app.UseMiddleware<Custom>();     // 9. Your custom middleware
app.MapGames();                  // 10. Endpoints (terminal — no next middleware)
```

**In this project (current state):**
```csharp
app.UseHttpLogging();   // logs method, path, status, duration

app.MapGames();
app.MapGetGenres();
app.MapGetRatings();
```

Exception handling, auth, and HTTPS redirection haven't been added yet — they'll come in later phases.

**Short-circuiting:**
Any middleware can stop the pipeline by not calling `await next(context)`. This is how auth middleware works — if the request isn't authenticated, it returns a `401` immediately and the endpoint never runs.

---
### Options Pattern

**The problem:**
Reading config with `builder.Configuration["Key"]` returns raw strings — no type safety, no IntelliSense, no validation, and the shape of the config is defined nowhere. Misspelling a key or forgetting a value fails silently at runtime, often only on the unlucky environment where it matters.

**What it does:**
The **Options pattern** binds a section of `appsettings.json` to a strongly-typed C# class and exposes it through DI. Config becomes a typed object with named properties; the shape lives in one place; the compiler catches typos; and validation can attach to the class.

**How it works:**

1. Define a class matching the config section shape:
```csharp
public class GameStoreOptions
{
    public int MaxGamePrice { get; set; }
    public string DefaultCurrency { get; set; } = "USD";
}
```

2. Add a matching section to `appsettings.json`:
```json
"GameStore": {
  "MaxGamePrice": 100,
  "DefaultCurrency": "USD"
}
```

3. Register and bind in `Program.cs`:
```csharp
builder.Services.Configure<GameStoreOptions>(
    builder.Configuration.GetSection("GameStore"));
```

4. Inject anywhere via `IOptions<T>`:
```csharp
app.MapGet("/config", (IOptions<GameStoreOptions> options) =>
{
    var settings = options.Value;
    return Results.Ok(settings.MaxGamePrice);
});
```

**Three `IOptions` variants:**

| Interface | Lifetime | Reloads? | Use when |
|-----------|----------|----------|----------|
| `IOptions<T>` | Singleton | No | Config never changes at runtime |
| `IOptionsSnapshot<T>` | Scoped (per request) | Yes | Need fresh config each request |
| `IOptionsMonitor<T>` | Singleton | Yes (via callback) | Want to react to config changes live |

**Note:** The Options pattern is not yet used in this project — configuration is currently read directly with `GetConnectionString()`. It becomes more valuable as the app grows and has more configuration sections to manage.

---
### IHttpContextAccessor — Accessing HttpContext Outside a Handler

**The problem:**
Inside a Minimal API handler, `HttpContext` is available as a parameter. But a service injected via DI (like a file uploader that needs the request's host to build a URL) has no handler parameter to read it from — and passing `HttpContext` through every method call manually is invasive and easy to forget.

**What it does:**
`IHttpContextAccessor` is a service that exposes the current request's `HttpContext` via an `AsyncLocal<T>`. Any class injected with it can reach the request context as long as it runs within an HTTP request — outside one (a background job, a startup hook), it returns `null`.

**In code:**
```csharp
// Register in Program.cs
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<FileUploader>();
```

```csharp
// Consumed in FileUploader.cs
public class FileUploader(IWebHostEnvironment environment, IHttpContextAccessor httpContextAccessor)
{
    public async Task<FileUploadResult> UploadFileAsync(IFormFile file, string folder)
    {
        var httpContext = httpContextAccessor.HttpContext;
        var fileUrl = $"{httpContext?.Request.Scheme}://{httpContext?.Request.Host}/{folder}/{safeFileName}";
        // ...
    }
}
```

`IHttpContextAccessor` stores the `HttpContext` in an `AsyncLocal<T>` — a slot that flows with the current async call chain. As long as you're within the same request, `.HttpContext` returns the right context. Outside of a request (e.g., in a background job) it returns `null`.

---
### launchSettings.json

**The problem:**
Different developers run the app differently — different ports, different environments, different launch profiles for IDE vs CLI vs IIS Express. Without a shared local-launch config, every teammate has to remember the right command-line flags for their machine.

**What it does:**
`launchSettings.json` (in `Properties/`) defines local-development launch profiles: which URL to bind, which environment to set, whether to launch a browser. The file is checked into version control but never deployed — production gets its config from environment variables, not this file.

**In code:**
```json
"http": {
    "applicationUrl": "http://localhost:5078",
    "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
    }
}
```

When `ASPNETCORE_ENVIRONMENT` is `"Development"`:
- `appsettings.Development.json` is loaded on top of `appsettings.json`
- More detailed error pages are shown
- Developer tools like hot reload are enabled

The app is accessible at `http://localhost:5078` during development.

---

