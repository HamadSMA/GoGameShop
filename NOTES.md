# GoGameShop — Learning Notes

> [!NOTE]
> These notes are AI-assisted and personally customized as I manually write code in this project. They serve as my reference and review material.

---

## Table of Contents

- [Project Setup and Configuration](#project-setup-and-configuration)
- [Data and EF Core](#data-and-ef-core)
- [Language and Patterns](#language-and-patterns)
- [API Design](#api-design)
- [Async Programming](#async-programming)

---

## Project Setup and Configuration

### ASP.NET Core & Minimal APIs

**What it is:**
ASP.NET Core is Microsoft's framework for building web applications and APIs with C#. A **Minimal API** is a lightweight way to define HTTP endpoints directly in code — without needing controllers, classes, or a lot of boilerplate.

**Why it's used:**
Traditional ASP.NET used "controllers" — classes with many methods. Minimal APIs skip that overhead and let you write endpoints directly, which is simpler and faster for APIs.

**How it fits:**
Every endpoint in this project (`MapGet`, `MapPost`, etc.) is a Minimal API. Instead of a `GamesController` class, there is a `MapGames()` method that registers all game-related routes.

---
### Program.cs — The Entry Point

**What it is:**
`Program.cs` is the starting point of a .NET application. Every .NET app has one. It's where you configure services (things the app needs) and the request pipeline (how requests are handled).

**Why it's used:**
.NET needs a single place to wire everything together — the database, routing, middleware, and startup logic all get registered here.

**How it fits:**
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

**What they are:**
`WebApplicationBuilder` is the object returned by `WebApplication.CreateBuilder(args)`. It's where you configure everything *before* the app starts — services, logging, configuration sources. Once you call `builder.Build()`, you get back a `WebApplication`, which is the running app itself.

```csharp
var builder = WebApplication.CreateBuilder(args);  // WebApplicationBuilder
// ... register services on builder ...
var app = builder.Build();                         // WebApplication
// ... map routes on app ...
app.Run();
```

**Why this two-object pattern exists:**
The separation is intentional. The builder phase is for *registration* — you're telling the DI container what exists. The app phase is for *usage* — you're consuming those registrations to configure the pipeline. Mixing the two would make it easy to accidentally use services before they're fully configured.

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

**What it is:**
The `.csproj` file (C# project file) is an XML file that defines the project's settings and dependencies. Think of it as the project's identity card.

**Why it's used:**
.NET needs to know what framework version you're targeting, what NuGet packages (libraries) you depend on, and what compiler options to use.

**How it fits in this project:**
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

**What it is:**
A JSON file that stores configuration values for your application — things like database connection strings, log levels, and feature flags.

**Why it's used:**
You don't want to hardcode values like database paths inside your code. Storing them in a config file means you can change them without recompiling. You can also have different configs per environment (Development, Production).

**How it fits:**
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

**What it is:**
ASP.NET Core has a built-in logging system. You inject an `ILogger<T>` into any class or endpoint and call methods like `LogInformation()`, `LogWarning()`, or `LogError()` to write log messages. `WebApplicationBuilder` sets this up automatically — you don't need to configure anything to get started.

**Why it's used:**
`Console.WriteLine` has no log level, no filtering, no structured output, and no way to route messages to different destinations (file, cloud, monitoring tools). The built-in logger does all of that, and is replaceable with third-party providers (Serilog, NLog, etc.) without changing the calling code.

**How it fits — injecting into an endpoint:**
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

**What it is:**
A C# feature that lets you declare `using` statements once in a single file (`GlobalUsings.cs`) and have them apply across every file in the project.

**Why it's used:**
Without global usings, you'd have to write `using GoGameShop.Api.Models;` at the top of every file that references a model. Global usings eliminate that repetition.

**How it fits:**
```csharp
global using GoGameShop.Api.Models;
global using GoGameShop.Api.Data;
global using GoGameShop.Api.Features.Games;
// ...etc
```

Now any file in the project can reference `Game`, `GoGameShopContext`, or `GetGamesEndpoint` without importing anything.

---
### Middleware

**What it is:**
Middleware is code that sits in the HTTP request pipeline and processes every request and response that passes through the application. Each piece of middleware can inspect or modify the request, call the next middleware in the chain, and then inspect or modify the response on the way back.

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

**What it is:**
The order in which you call `app.Use*()` in `Program.cs` is the order middleware executes on the way in, and the reverse on the way out. Getting this wrong causes bugs — for example, putting authorization before routing means the router never ran, so there's no endpoint to authorize.

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

**What it is:**
The Options pattern binds a section of `appsettings.json` to a strongly-typed C# class, and makes that class available via dependency injection. It's the recommended way to consume configuration in ASP.NET Core — instead of reading raw strings with `builder.Configuration["Key"]`, you work with a typed object.

**Why it's used:**
Raw configuration strings have no type safety, no validation, and no IntelliSense. A typed options class gives you all three, and centralizes where config shape is defined.

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

**What it is:**
`IHttpContextAccessor` is a service that lets you access the current `HttpContext` from anywhere in your code — not just inside a route handler where `HttpContext` is available as a direct parameter.

**Why it's used:**
Inside a Minimal API handler, `HttpContext` can be injected as a parameter automatically. But inside a service class (like `FileUploader`) that is resolved from DI, there's no handler parameter — you need `IHttpContextAccessor` to reach the current request's context from within the service.

**How it fits:**
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

**What it is:**
A development-only configuration file in the `Properties/` folder that defines how the app starts when you run `dotnet run` or launch from an IDE.

**Why it's used:**
It specifies the URL the app listens on, the environment (Development/Production), and whether to open a browser automatically. This file is never deployed — it's only for local development.

**How it fits:**
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

## Data and EF Core

### Models & Entities

**What it is:**
A **model** (also called an **entity**) is a C# class that represents a real-world thing your app works with. In EF Core, each model maps to a database table.

**Why it's used:**
Models let you work with data as objects (e.g., a `Game` object) rather than writing raw SQL. EF Core translates your C# operations into database queries automatically.

**How it fits — `Game.cs`:**
```csharp
public class Game
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public Genre? Genre { get; set; }      // Navigation property
    public Guid GenreId { get; set; }      // Foreign key
    public Rating? Rating { get; set; }    // Navigation property
    public Guid RatingId { get; set; }     // Foreign key
    public DateOnly ReleaseDate { get; set; }
    public decimal Price { get; set; }
    public required string Description { get; set; }
}
```

Key concepts here:
- **`Guid Id`**: A globally unique identifier used as the primary key (safer than auto-increment integers for APIs).
- **`required`**: A C# keyword that forces anyone creating a `Game` to provide a value for this property.
- **Navigation property** (`Genre? Genre`): A reference to the related `Genre` object — EF Core can load this automatically.
- **Foreign key** (`Guid GenreId`): The actual column stored in the database that links to the `Genres` table.
- **`?` (nullable)**: The `?` means this can be null. `Genre?` means the Genre might not be loaded yet.

---
### Entity Framework Core (EF Core)

**What it is:**
EF Core is an **Object-Relational Mapper (ORM)**. It lets you interact with a database using C# objects and LINQ instead of writing raw SQL queries.

**Why it's used:**
Writing raw SQL is tedious and error-prone. EF Core automatically translates your C# code like:
```csharp
dbContext.Games.Where(g => g.Price < 30).ToList()
```
into SQL like:
```sql
SELECT * FROM Games WHERE Price < 30
```

It also handles:
- Creating and updating database schemas (migrations)
- Tracking changes to objects so it knows what to save
- Managing relationships between tables

**How it fits:**
This project uses EF Core with the SQLite provider. The three tables (Games, Genres, Ratings) are entirely managed by EF Core — no SQL was written manually.

**Common EF Core queries and their SQL counterparts:**

**Get all rows**
```csharp
await dbContext.Games.ToListAsync();
```
```sql
SELECT * FROM Games;
```

---

**Filter rows**
```csharp
await dbContext.Games.Where(g => g.Price < 30).ToListAsync();
```
```sql
SELECT * FROM Games WHERE Price < 30;
```

---

**Get one by primary key**
```csharp
await dbContext.Games.FindAsync(id);
```
```sql
SELECT * FROM Games WHERE Id = @id LIMIT 1;
```

---

**Get first match (throws if none found)**
```csharp
await dbContext.Games.FirstAsync(g => g.Name == "Halo");
```
```sql
SELECT * FROM Games WHERE Name = 'Halo' LIMIT 1;
```

---

**Get first match or null**
```csharp
await dbContext.Games.FirstOrDefaultAsync(g => g.Name == "Halo");
```
```sql
SELECT * FROM Games WHERE Name = 'Halo' LIMIT 1;
-- returns NULL if no row matches
```

---

**Select specific columns (projection)**
```csharp
await dbContext.Games.Select(g => new { g.Id, g.Name }).ToListAsync();
```
```sql
SELECT Id, Name FROM Games;
```

---

**Join / load related data (eager loading)**
```csharp
await dbContext.Games.Include(g => g.Genre).ToListAsync();
```
```sql
SELECT Games.*, Genres.*
FROM Games
INNER JOIN Genres ON Games.GenreId = Genres.Id;
```

---

**Order results**
```csharp
await dbContext.Games.OrderBy(g => g.Price).ToListAsync();
await dbContext.Games.OrderByDescending(g => g.Price).ToListAsync();
```
```sql
SELECT * FROM Games ORDER BY Price ASC;
SELECT * FROM Games ORDER BY Price DESC;
```

---

**Count rows**
```csharp
await dbContext.Games.CountAsync();
await dbContext.Games.CountAsync(g => g.Price < 30);
```
```sql
SELECT COUNT(*) FROM Games;
SELECT COUNT(*) FROM Games WHERE Price < 30;
```

---

**Check if any row exists**
```csharp
await dbContext.Games.AnyAsync(g => g.GenreId == id);
```
```sql
SELECT CASE WHEN EXISTS (
    SELECT 1 FROM Games WHERE GenreId = @id
) THEN 1 ELSE 0 END;
```

---

**Insert a row**
```csharp
dbContext.Games.Add(game);
await dbContext.SaveChangesAsync();
```
```sql
INSERT INTO Games (Id, Name, GenreId, RatingId, Price, ReleaseDate, Description)
VALUES (@Id, @Name, @GenreId, @RatingId, @Price, @ReleaseDate, @Description);
```

---

**Update a row**
```csharp
var game = await dbContext.Games.FindAsync(id);
game.Price = 19.99m;
await dbContext.SaveChangesAsync();
```
```sql
UPDATE Games SET Price = 19.99 WHERE Id = @id;
```

---

**Delete a row (load then remove)**
```csharp
var game = await dbContext.Games.FindAsync(id);
dbContext.Games.Remove(game);
await dbContext.SaveChangesAsync();
```
```sql
DELETE FROM Games WHERE Id = @id;
```

---

**Delete without loading (bulk/direct)**
```csharp
await dbContext.Games.Where(g => g.Id == id).ExecuteDeleteAsync();
```
```sql
DELETE FROM Games WHERE Id = @id;
-- No SELECT round-trip — single statement
```

---

**Pagination (skip & take)**
```csharp
await dbContext.Games.Skip(20).Take(10).ToListAsync();
```
```sql
SELECT * FROM Games LIMIT 10 OFFSET 20;
```

---
### DbContext

**What it is:**
`DbContext` is the central class in EF Core. It represents a session with the database and gives you access to your tables through `DbSet<T>` properties.

**Why it's used:**
You need one place that knows about all your tables, manages the database connection, and coordinates saving changes. That's the `DbContext`.

**How it fits — `GoGameShopContext.cs`:**
```csharp
public class GoGameShopContext(DbContextOptions<GoGameShopContext> options)
    : DbContext(options)
{
    public DbSet<Game> Games => Set<Game>();
    public DbSet<Genre> Genres => Set<Genre>();
    public DbSet<Rating> Ratings => Set<Rating>();
}
```

- The constructor takes `DbContextOptions` — this is how the connection string and database provider (SQLite) get passed in.
- `DbSet<Game>` represents the `Games` table. You query it like `dbContext.Games.Where(...)`.
- This class is registered as a service in `Program.cs` with `builder.Services.AddSqlite<GoGameShopContext>(connectionString)` and injected into endpoints automatically.

---
### Database Migrations

**What it is:**
A migration is a snapshot of your database schema at a point in time. EF Core generates them automatically when you change your models, and applies them to create or update the real database.

**Why it's used:**
Your database schema needs to stay in sync with your C# models. Migrations track every change (add column, create table, add index) as versioned code files that can be applied in order.

**How it fits:**
The migration at `Migrations/20260324172356_InitialCreate.cs` created the three tables:

```csharp
migrationBuilder.CreateTable(name: "Genres", ...)
migrationBuilder.CreateTable(name: "Ratings", ...)
migrationBuilder.CreateTable(name: "Games",
    columns: table => new {
        Id, Name, GenreId, RatingId, ReleaseDate, Price, Description
    },
    constraints: table => {
        table.PrimaryKey("PK_Games", x => x.Id);
        table.ForeignKey("FK_Games_Genres_GenreId", x => x.GenreId, "Genres", "Id", onDelete: ReferentialAction.Cascade);
        table.ForeignKey("FK_Games_Ratings_RatingId", x => x.RatingId, "Ratings", "Id", onDelete: ReferentialAction.Cascade);
    }
);
```

- **Foreign Keys**: `GenreId` and `RatingId` link Games to Genres and Ratings tables. `onDelete: Cascade` means deleting a Genre also deletes all its Games.
- Migrations are applied on startup via `await dbContext.Database.MigrateAsync()` in `DataExtensions.cs`.

**Common CLI commands:**
```bash
dotnet ef migrations add <MigrationName>   # Generate a new migration
dotnet ef database update                  # Apply pending migrations
```

---
### Database Seeding

**What it is:**
Seeding means pre-populating the database with initial data when the app first starts.

**Why it's used:**
A fresh database is empty. For the app to be useful right away (and for testing), you need some starting data — like the list of genres and age ratings.

**How it fits — `DataExtensions.cs`:**
```csharp
private static async Task SeedDbAsync(this WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<GoGameShopContext>();

    if (!dbContext.Genres.Any())  // Only seed if the table is empty
    {
        dbContext.Genres.AddRange(
            new Genre { Name = "Action" },
            new Genre { Name = "RPG" },
            // ...
        );
    }
    await dbContext.SaveChangesAsync();
}
```

- `if (!dbContext.Genres.Any())` prevents re-seeding on every restart.
- `using var scope = app.Services.CreateScope()` is needed because `DbContext` is a scoped service — it must be resolved inside a scope, not at startup level.
- `await dbContext.SaveChangesAsync()` commits the inserts asynchronously — the thread is freed while the database write happens.

---
### AsNoTracking

**What it is:**
By default, EF Core tracks every entity it loads — it keeps a copy in memory to detect changes when you call `SaveChanges()`. `AsNoTracking()` disables this for a query.

**Why it's used:**
Tracking has a memory and CPU cost. For read-only queries where you'll never update the data, tracking is wasted overhead. `AsNoTracking()` makes read queries faster and lighter.

**How it fits:**
```csharp
dbContext.Games
    .Include(game => game.Genre)
    .Include(game => game.Rating)
    .Select(game => new GameSummaryDto(...))
    .AsNoTracking()   // Tell EF Core: "don't track these, we're just reading"
```

Used in `GET /games`, `GET /genres`, and `GET /ratings` — all pure read operations that return DTOs and never update the entities.

---
### ExecuteDelete

**What it is:**
`ExecuteDelete()` is an EF Core method that translates directly into a SQL `DELETE` statement without loading the entity into memory first.

**Why it's used:**
The traditional EF Core delete pattern requires: load entity → call `Remove()` → call `SaveChanges()`. That's two database round trips. `ExecuteDelete()` does it in one SQL statement.

**How it fits — `DeleteGameEndpoint`:**
```csharp
await dbContext.Games
    .Where(game => game.Id == id)
    .ExecuteDeleteAsync();
```

This generates: `DELETE FROM Games WHERE Id = @id`

No entity is loaded into memory — it's a direct, efficient delete. `ExecuteDeleteAsync()` is the async version, returning a `Task` so the thread is free while the database processes the statement.

---
### Include (Eager Loading)

**What it is:**
By default, EF Core does not load related entities (navigation properties). **Eager loading** with `Include()` tells it to load them in the same query using a SQL `JOIN`.

**Why it's used:**
The `Game` entity has `Genre` and `Rating` as navigation properties. If you don't include them, they'll be `null`. `Include()` joins the related tables so the data is available.

**How it fits — `GET /games`:**
```csharp
dbContext.Games
    .Include(game => game.Genre)    // JOIN Genres table
    .Include(game => game.Rating)   // JOIN Ratings table
    .Select(game => new GameSummaryDto(
        game.Id,
        game.Name,
        game.Genre!.Name,    // Now accessible because of Include
        game.Rating!.Name,   // Now accessible because of Include
        ...
    ))
```

The `!` (null-forgiving operator) tells the compiler "I know this won't be null here" — it's safe because `Include()` guarantees the navigation property is loaded.

**The three loading strategies in EF Core:**
- **Eager loading** (`Include`) — load related data with the main query (used here)
- **Explicit loading** — load related data on demand with a separate query
- **Lazy loading** — automatically load when accessed (requires proxy setup, not used here)

---

## Language and Patterns

### Extension Methods

**What it is:**
An extension method is a static method that "adds" new methods to an existing type without modifying its source code. You define it with the `this` keyword in the parameter list.

**Why it's used:**
They allow clean, fluent API design. Instead of calling `GamesEndpoints.MapGames(app)`, you can call `app.MapGames()` — as if `MapGames` was always a method on `WebApplication`.

**How it fits:**
```csharp
// Defined as:
public static void MapGames(this IEndpointRouteBuilder app) { ... }

// Called as:
app.MapGames();  // Reads naturally — "app, map the games routes"
```

This pattern is used everywhere:
- `app.MapGames()` — registers game endpoints
- `app.InitializeDbAsync()` — runs migrations and seeds (async)
- `dbContext.Games.AsNoTracking()` — disables tracking (built into EF Core)

---
### Dependency Injection

**What it is:**
Dependency Injection (DI) is a design pattern where objects receive their dependencies (things they need) from the outside rather than creating them themselves. In ASP.NET Core, a built-in DI container manages this automatically.

**Why it's used:**
Without DI, every endpoint would need to manually create a `DbContext`, manage its lifecycle, and dispose of it. DI handles all of that — you just declare what you need, and the framework provides it.

**How it fits:**
Services are registered in `Program.cs`:
```csharp
builder.Services.AddSqlite<GoGameShopContext>(connectionString);
```

Then any endpoint can request a `GoGameShopContext` just by listing it as a parameter:
```csharp
app.MapGet("/", (GoGameShopContext dbContext) =>
    dbContext.Games.ToList()
);
```

ASP.NET Core sees the `GoGameShopContext` parameter and automatically creates and injects one per request. When the request ends, it disposes of it.

---
### C# Records

**What it is:**
A `record` is a special C# type designed for immutable data objects. It auto-generates equality comparison, `ToString()`, and a constructor based on the properties you declare.

**Why it's used:**
DTOs are perfect candidates for records because they carry data without behavior — you create them, read from them, and throw them away. The concise syntax saves boilerplate.

**How it fits:**
```csharp
// Record - very concise, immutable
public record GameSummaryDto(Guid Id, string Name, string Genre, string Rating, decimal Price, DateOnly ReleaseDate);

// Equivalent class - verbose
public class GameSummaryDto
{
    public Guid Id { get; init; }
    public string Name { get; init; }
    // ...constructor, Equals, GetHashCode, ToString...
}
```

The record syntax on one line replaces what would be 30+ lines of a class.

---
### Delegates, Func & Action

**What a delegate is:**
A delegate is a type that holds a reference to a method — it's essentially a variable that stores a function. You can pass it around, store it, and call it later just like calling the original method.

```csharp
// Declare a delegate type
delegate int MathOperation(int a, int b);

// Assign a method to it
MathOperation add = (a, b) => a + b;

// Call it
int result = add(3, 5);  // 8
```

**Why it matters:**
Delegates are the foundation of callbacks, event handling, and LINQ. Every time you write a lambda like `game => game.Price < 30`, you're creating a delegate. The reason `Where()`, `Select()`, `Include()`, and `OrderBy()` can accept lambdas is because their parameters are delegate types (`Func<T, bool>`, `Func<T, TResult>`, etc.).

---

**`Func<>` — delegates that return a value:**

`Func<T, TResult>` is a built-in delegate type for functions that take input and return a result. The last type parameter is always the return type.

```csharp
Func<int, int, int>  add     = (a, b) => a + b;      // takes 2 ints, returns int
Func<Game, bool>     isChap  = game => game.Price < 20; // takes Game, returns bool
Func<Game, string>   getName = game => game.Name;      // takes Game, returns string
```

These appear constantly in EF Core:
```csharp
dbContext.Games.Where(game => game.Price < 30)
//              ^ Func<Game, bool>

dbContext.Games.Select(game => game.Name)
//              ^ Func<Game, string>

dbContext.Games.OrderBy(game => game.Price)
//               ^ Func<Game, decimal>
```

---

**`Action<>` — delegates that return nothing:**

`Action<T>` is a built-in delegate type for methods that take input but return `void`.

```csharp
Action<string>       print  = message => Console.WriteLine(message);
Action<Game, string> log    = (game, msg) => Console.WriteLine($"{game.Name}: {msg}");
Action               greet  = () => Console.WriteLine("Hello");  // no parameters
```

Used in things like `List.ForEach`:
```csharp
games.ForEach(game => Console.WriteLine(game.Name));
//            ^ Action<Game>
```

---

**Lambda expressions:**

A lambda is the shorthand syntax for creating a delegate inline. The `=>` is read as "goes to":

```csharp
// Full method
int Add(int a, int b) { return a + b; }

// Equivalent lambda
(int a, int b) => a + b

// Type-inferred lambda (compiler figures out the types from context)
(a, b) => a + b
```

Single-parameter lambdas don't need parentheses:
```csharp
game => game.Price < 30
```

**In this project, lambdas appear everywhere:**
```csharp
.Where(game => game.Id == id)              // filter predicate
.Include(game => game.Genre)               // navigation selector
.Select(game => new GameSummaryDto(...))   // projection
.OrderBy(game => game.Price)               // sort key
```

---
### Generics

**What they are:**
Generics let you write a class, method, or interface that works with *any* type, specified later by the caller. The type parameter is written in angle brackets: `<T>`.

```csharp
// Without generics — only works for int
int[] intArray = new int[5];

// With generics — works for any type
List<int>    numbers = new List<int>();
List<string> names   = new List<string>();
List<Game>   games   = new List<Game>();
```

The `T` is just a placeholder name — it could be `T`, `TResult`, `TEntity`, or anything. By convention `T` is used for a single type parameter.

**Why they're used:**
Without generics you'd need a separate `GameList`, `GenreList`, `RatingList` class for each type. With generics, one `List<T>` covers all of them. You get type safety (the compiler knows what's inside) without sacrificing reusability.

**How they appear in this project:**

`DbSet<T>` — EF Core's table accessor is generic:
```csharp
public DbSet<Game>   Games   => Set<Game>();
public DbSet<Genre>  Genres  => Set<Genre>();
public DbSet<Rating> Ratings => Set<Rating>();
```

`AddSqlite<T>` — registers the specific DbContext type:
```csharp
builder.Services.AddSqlite<GoGameShopContext>(connectionString);
```

`GetRequiredService<T>` — resolves a specific type from the DI container:
```csharp
scope.ServiceProvider.GetRequiredService<GoGameShopContext>();
```

`Task<T>` — an async operation that produces a specific result type:
```csharp
Task<Game?>        // async operation that returns a nullable Game
Task<List<Game>>   // async operation that returns a list of Games
```

`ILogger<T>` — logger scoped to a specific class:
```csharp
ILogger<Program>   // logger whose category name is "Program"
```

**Generic methods:**

A method can also be generic, with the type parameter on the method itself:

```csharp
// Generic method — T is determined by the caller
T GetFirst<T>(List<T> items) => items[0];

// Calling it — compiler infers T from the argument
string first = GetFirst(new List<string> { "a", "b" });  // T = string
Game   game  = GetFirst(new List<Game> { ... });          // T = Game
```

**Generic constraints (`where T : ...`):**

You can restrict what types are allowed as `T`:

```csharp
// T must be a class (reference type)
void Save<T>(T entity) where T : class { ... }

// T must implement an interface
void Print<T>(T item) where T : IFormattable { ... }

// T must have a parameterless constructor
T Create<T>() where T : new() => new T();
```

EF Core's `DbContext.Set<T>()` uses `where T : class` — it only works with reference types, not primitives.

---
### Exception Handling

When something goes wrong at runtime — a null value where one wasn't expected, a database that can't be reached, a value that's out of range — C# surfaces that as an **exception**. An exception is an object that carries information about the failure: what went wrong, where, and a full stack trace. If you don't handle it, the runtime unwinds the call stack and crashes the request (or the whole app).

The mechanism for handling exceptions is `try/catch/finally`:

```csharp
try
{
    // Code that might throw
    var game = await dbContext.Games.FindAsync(id);
    game.Price = newPrice;  // throws NullReferenceException if game is null
    await dbContext.SaveChangesAsync();
}
catch (NullReferenceException ex)
{
    // Runs only if a NullReferenceException was thrown
    logger.LogError("Game not found: {Message}", ex.Message);
}
finally
{
    // Always runs — whether an exception was thrown or not
    stopwatch.Stop();
}
```

The `finally` block is guaranteed to run regardless of what happens in `try` — on success, on exception, even if a `return` statement exits early. This makes it the right place for cleanup that must always happen: stopping a timer, closing a file, releasing a resource. This is exactly how `RequestTimingMiddleware` in this project works — the stopwatch log is in `finally` so it fires even if the next middleware throws.

---

**Catching specific exceptions**

You can stack multiple `catch` blocks to handle different failure types differently. The runtime checks them top to bottom and runs the first one that matches — so always put specific exceptions before general ones:

```csharp
try
{
    var value = int.Parse(input);
}
catch (FormatException ex)
{
    // input was not a valid number
}
catch (OverflowException ex)
{
    // input was a valid number but too large for int
}
catch (Exception ex)
{
    // catch-all — handles anything not caught above
    // be careful: this swallows every possible exception
}
```

If you put `catch (Exception ex)` first, it will match everything and the specific blocks below it will never run. The compiler won't stop you — it'll just silently swallow exceptions you didn't intend to catch.

---

**The `when` filter**

`when` lets you add a condition to a `catch` block. If the condition is false, the block is skipped and the exception keeps propagating — as if that `catch` wasn't there:

```csharp
catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
{
    // only handles 404s — other HttpRequestExceptions are not caught here
}
```

---

**Re-throwing**

If you catch an exception but can't fully handle it, you can re-throw it. There's an important difference:

```csharp
catch (Exception ex)
{
    logger.LogError(ex, "Something went wrong");

    throw;      // re-throws the original exception, preserving the full stack trace

    throw ex;   // DON'T do this — resets the stack trace to this line,
                // making the original call site invisible in logs
}
```

Always use bare `throw`, never `throw ex`. Losing the original stack trace makes debugging significantly harder.

---

**Common exceptions and when you encounter them**

| Exception | When it happens |
|-----------|----------------|
| `NullReferenceException` | You called a method or accessed a property on a `null` object |
| `ArgumentNullException` | You passed `null` to a parameter that doesn't allow it |
| `ArgumentException` | You passed an invalid argument (wrong format, out-of-range value) |
| `ArgumentOutOfRangeException` | An index or value is outside the allowed range |
| `InvalidOperationException` | You called a method at the wrong time (e.g., reading from a closed stream) |
| `KeyNotFoundException` | You looked up a key in a dictionary that doesn't exist |
| `NotImplementedException` | A method has no implementation yet — placeholder, should never reach production |
| `FormatException` | A string couldn't be parsed into the expected type (`int.Parse("abc")`) |
| `OverflowException` | A numeric operation exceeded the type's limit |
| `IOException` | A file or stream operation failed |
| `DbUpdateException` | EF Core couldn't save changes — constraint violation, connection issue, etc. |
| `OperationCanceledException` | An async operation was cancelled (e.g., client disconnected) |

---

**Don't use exceptions for control flow**

Exceptions are expensive — throwing one allocates an object and unwinds the stack. More importantly, code that uses exceptions as an expected branch is hard to read:

```csharp
// BAD — using exception as an if/else
try
{
    var game = dbContext.Games.Single(g => g.Id == id);
    return Results.Ok(game);
}
catch (InvalidOperationException)
{
    return Results.NotFound();
}

// GOOD — use the method designed for this case
var game = await dbContext.Games.FindAsync(id);
return game is null ? Results.NotFound() : Results.Ok(game);
```

Reserve exceptions for genuinely unexpected failures — things that shouldn't happen in normal operation. For expected cases like "record not found", use return values (`null`, `bool`, `Result` types) instead.

---
### Fail-Fast Validation

**What it is:**
Fail-fast validation means checking preconditions **at the top of a method** and returning early if they aren't met — before doing any real work. Each guard clause checks one condition, sets an error, and returns immediately.

**Why it's used:**
- Keeps the "happy path" — the code that actually does the work — unindented and easy to read
- Avoids wasting resources (disk I/O, database calls) on invalid input
- Makes each failure reason explicit and self-contained

**How it fits:**
In `FileUploader.UploadFileAsync`, three guards run before any file is written to disk:

```csharp
public async Task<FileUploadResult> UploadFileAsync(IFormFile file, string folder)
{
    var result = new FileUploadResult();

    // Guard 1 — is there actually a file?
    if (file == null || file.Length == 0)
    {
        result.IsSucess = false;
        result.ErrorMessage = "File not found";
        return result;  // exit immediately
    }

    // Guard 2 — is the file too large?
    if (file.Length > 10 * 1024 * 1024)
    {
        result.IsSucess = false;
        result.ErrorMessage = "File size is too large";
        return result;  // exit immediately
    }

    // Guard 3 — is the extension allowed?
    string[] permittedExtensions = [".jpg", ".jpeg", ".png"];
    var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (string.IsNullOrEmpty(fileExtension) || !permittedExtensions.Contains(fileExtension))
    {
        result.IsSucess = false;
        result.ErrorMessage = "Unsupported file type";
        return result;  // exit immediately
    }

    // --- Happy path: all guards passed ---
    // save file, build URL, return success...
}
```

**Comparison with alternative validation approaches:**

| Approach | How it works | When to use |
|---|---|---|
| **Fail-fast (guard clauses)** | Return early on the first failure; only one error reported | Simple methods; quick rejection before expensive work |
| **Exception throwing** | `throw new ArgumentException(...)` instead of returning a result | For programming errors that should never happen (bugs, not user input) |
| **Collect-all errors** | Run all checks, gather every failure, return them together | Form validation where the user needs to fix multiple things at once |
| **FluentValidation / Data Annotations** | Declare rules on a class; the framework runs them automatically | DTO/model validation at the API boundary (see [Data Annotations & Validation](#data-annotations--validation)) |

The approach in `FileUploader` is fail-fast + result object (not exceptions), because these are **expected, recoverable failures** caused by user input — not bugs. Exceptions are reserved for genuinely unexpected situations.

---
### Vertical Slice Architecture

**What it is:**
Vertical slicing organizes code by **feature** rather than by **technical layer**. Instead of folders named `Controllers/`, `Services/`, `Repositories/`, you have folders named by what they do.

**Why it's used:**
When you work on a feature (e.g., "Create Game"), all the relevant code — the endpoint, DTO, validation — is in one folder. You don't have to jump between multiple layers across the project.

**How it fits:**
```
Features/
├── Games/
│   ├── GetGames/
│   │   ├── GetGamesEndpoint.cs    # The route handler
│   │   └── GetGamesDtos.cs        # The DTOs for this feature
│   ├── GetGame/
│   ├── CreateGame/
│   ├── UpdateGame/
│   └── DeleteGame/
├── Genres/
│   ├── GetGenresEndpoint.cs
│   └── GetGenresDtos.cs
└── Ratings/
    ├── GetRatingsEndpoint.cs
    └── GetRatingsDtos.cs
```

Each folder is a self-contained "slice" of the application. Adding a new feature means adding a new folder, not modifying multiple existing layers.

---
### LINQ (Language Integrated Query)

**What it is:**
LINQ is a set of C# methods for filtering, transforming, and aggregating collections — arrays, lists, database results, anything that implements `IEnumerable<T>` or `IQueryable<T>`.

**Why it's used:**
Instead of writing `foreach` loops to search or reshape data, LINQ lets you express *what* you want in a readable, chainable style. EF Core also translates LINQ calls directly into SQL.

**Methods:**

`All(predicate)` — returns `true` only if every item matches the condition:
```csharp
bool allCheap = games.All(g => g.Price < 100);
// true only if every game costs less than 100
```

`Sum(selector)` — adds up a numeric property across all items:
```csharp
decimal total = games.Sum(g => g.Price);
```

`Min(selector)` / `Max(selector)` — finds the smallest or largest value of a property:
```csharp
decimal cheapest = games.Min(g => g.Price);
decimal priciest = games.Max(g => g.Price);
```

**Chaining:**
LINQ methods can be chained — each one returns a new collection that the next method operates on:
```csharp
var results = games
    .Where(g => g.Price < 60)      // filter first
    .OrderBy(g => g.Name)          // then sort
    .Select(g => new { g.Name, g.Price }); // then reshape
```

**Deferred vs immediate execution:**
Most LINQ methods are *deferred* — the query only runs when you iterate (e.g. `foreach`, `ToList()`). Methods like `Count()`, `First()`, `Any()`, and `Sum()` execute immediately.

---
### Common C# Built-in Methods

**What they are:**
The .NET standard library ships with utility methods on strings, collections, arrays, and more. These are the ones you'll reach for constantly.

---

**String methods:**

`ToLower()` / `ToUpper()` — converts all characters to lowercase or uppercase:
```csharp
"Hello".ToLower() // "hello"
"Hello".ToUpper() // "HELLO"
```

`Trim()` — removes leading and trailing whitespace:
```csharp
"  hello  ".Trim() // "hello"
```

`Contains(value)` — returns `true` if the string includes the given substring:
```csharp
"Hello, World".Contains("World") // true
```

`StartsWith(value)` / `EndsWith(value)` — checks whether the string begins or ends with a given value:
```csharp
"Hello".StartsWith("He") // true
"Hello".EndsWith("lo")   // true
```

`Replace(old, new)` — swaps every occurrence of one substring with another:
```csharp
"Hello World".Replace("World", "C#") // "Hello C#"
```

`Split(separator)` — breaks the string into an array at each separator:
```csharp
"a,b,c".Split(",") // ["a", "b", "c"]
```

`Substring(startIndex, length)` — extracts a portion of the string starting at an index:
```csharp
"Hello World".Substring(6, 5) // "World"
```

`IndexOf(value)` — returns the position of the first occurrence of a substring, or `-1` if not found:
```csharp
"Hello World".IndexOf("World") // 6
```

`string.IsNullOrEmpty(s)` — returns `true` if the string is `null` or has zero characters:
```csharp
string.IsNullOrEmpty("")    // true
string.IsNullOrEmpty(null)  // true
string.IsNullOrEmpty("hi")  // false
```

`string.IsNullOrWhiteSpace(s)` — same as above, but also returns `true` for strings that are only spaces:
```csharp
string.IsNullOrWhiteSpace("   ") // true
```

`string.Join(separator, collection)` — combines a collection of strings into one, with a separator between each:
```csharp
string.Join(", ", new[] { "a", "b", "c" }) // "a, b, c"
```

---

**List / collection methods:**

`Add(item)` — appends an item to the end of the list:
```csharp
list.Add(9);
```

`Remove(item)` — removes the first occurrence of the given item:
```csharp
list.Remove(1); // removes the first 1 it finds
```

`Contains(item)` — returns `true` if the item exists in the list:
```csharp
list.Contains(4) // true
```

`Count` — a property (not a method) that returns the number of items:
```csharp
list.Count // 5
```

`Clear()` — removes all items from the list:
```csharp
list.Clear(); // list is now empty
```

`Sort()` — sorts the list in place (modifies the original list, returns nothing):
```csharp
list.Sort(); // [1, 1, 3, 4, 5]
```

`Reverse()` — reverses the order of items in place:
```csharp
list.Reverse(); // [5, 4, 3, 1, 1]
```

`ToArray()` / `ToList()` — converts between a list and an array:
```csharp
int[] arr = list.ToArray();
List<int> back = arr.ToList();
```

---

**Math methods:**

`Math.Abs(n)` — returns the absolute (positive) value of a number:
```csharp
Math.Abs(-5) // 5
```

`Math.Round(n, decimals)` — rounds to the specified number of decimal places:
```csharp
Math.Round(3.567, 2) // 3.57
```

`Math.Floor(n)` — rounds *down* to the nearest whole number:
```csharp
Math.Floor(3.9) // 3
```

`Math.Ceiling(n)` — rounds *up* to the nearest whole number:
```csharp
Math.Ceiling(3.1) // 4
```

`Math.Max(a, b)` / `Math.Min(a, b)` — returns the larger or smaller of two values:
```csharp
Math.Max(10, 20) // 20
Math.Min(10, 20) // 10
```

`Math.Pow(base, exponent)` — raises a number to a power:
```csharp
Math.Pow(2, 10) // 1024
```

`Math.Sqrt(n)` — returns the square root:
```csharp
Math.Sqrt(16) // 4
```

---

**Convert / parse:**

`int.TryParse(s, out result)` — tries to convert a string to an int. Returns `true`/`false` instead of throwing, and puts the result in the `out` variable:
```csharp
bool ok = int.TryParse("abc", out int n); // ok = false, n = 0
bool ok2 = int.TryParse("42", out int m); // ok2 = true, m = 42
```

`ToString()` — converts any value to its string representation. Available on every type:
```csharp
42.ToString()   // "42"
3.14.ToString() // "3.14"
true.ToString() // "True"
```

---

## API Design

### Route Groups

**What it is:**
A route group lets you apply a common URL prefix to a set of endpoints, so you don't repeat it on every single one.

**Why it's used:**
Instead of writing `/games` in every game endpoint, you define the prefix once and all endpoints under the group inherit it.

**How it fits — `GamesEndpoints.cs`:**
```csharp
public static void MapGames(this IEndpointRouteBuilder app)
{
    var games = app.MapGroup("/games");  // All routes below are prefixed with /games

    games.MapGetGames();    // GET /games
    games.MapGetGame();     // GET /games/{id}
    games.MapCreateGame();  // POST /games
    games.MapUpdateGame();  // PUT /games/{id}
    games.MapDeleteGame();  // DELETE /games/{id}
}
```

Each child endpoint only defines its relative path (e.g., `/` or `/{id}`), not the full path.

---
### DTOs (Data Transfer Objects)

**What it is:**
A DTO is a simple object used to transfer data between layers — specifically between your API and its clients. It defines exactly what data to accept (input) or return (output), separate from your database model.

**Why it's used:**
You don't want to expose your full database model directly because:
- Your model may have fields you don't want to expose (e.g., internal IDs, passwords)
- Input and output shapes are often different
- Validation should happen on the DTO, not the model

**How it fits — two types used here:**

**Summary DTO** (for listing — returns minimal data):
```csharp
public record GameSummaryDto(Guid Id, string Name, string Genre, string Rating, decimal Price, DateOnly ReleaseDate);
```

**Details DTO** (for single game — returns full data):
```csharp
public record GameDetailsDto(Guid Id, string Name, Guid GenreId, Guid RatingId, decimal Price, DateOnly ReleaseDate, string Description);
```

**Create DTO** (for creating — accepts input with validation):
```csharp
public record CreateGameDto([Required][StringLength(50)] string Name, Guid GenreId, ...);
```

Notice that `GameSummaryDto` returns `Genre` as a `string` (the genre name), while `GameDetailsDto` returns `GenreId` as a `Guid`. The summary flattens relationships for easy display; the details DTO returns IDs so a client can edit the game.

---
### Data Annotations & Validation

**What it is:**
Data annotations are attributes (markers in square brackets) you put on DTO properties to declare validation rules. The framework enforces them automatically before your endpoint code runs.

**Why it's used:**
You should never trust input from clients. Validation ensures the data is in the correct shape before you try to save it to the database.

**How it fits — `CreateGameDto`:**
```csharp
public record CreateGameDto(
    [Required][StringLength(50)] string Name,      // Must be provided, max 50 chars
    Guid GenreId,
    Guid RatingId,
    DateOnly ReleaseDate,
    [Range(1, 100)] decimal Price,                 // Must be between $1 and $100
    [Required][StringLength(500)] string Description  // Required, max 500 chars
);
```

`builder.Services.AddValidation()` in `Program.cs` enables automatic validation. If a request fails validation, the framework returns a `400 Bad Request` response automatically — your endpoint code never even runs.

Common annotations:
- `[Required]` — value must be present and non-empty
- `[StringLength(50)]` — string can be at most 50 characters
- `[Range(1, 100)]` — number must be within this range
- `[EmailAddress]` — must be a valid email format

---
### CRUD Endpoints

**What it is:**
CRUD stands for **Create, Read, Update, Delete** — the four fundamental operations on data. REST APIs map these to HTTP verbs: `POST`, `GET`, `PUT`/`PATCH`, `DELETE`.

**Why it's used:**
Almost every data-driven app needs these four operations. Following REST conventions makes your API predictable and easy for others to use.

**How it fits:**

| HTTP Verb | Route | Operation | Endpoint |
|-----------|-------|-----------|----------|
| `GET` | `/games` | Read all | `GetGamesEndpoint` |
| `GET` | `/games/{id}` | Read one | `GetGameEndpoint` |
| `POST` | `/games` | Create | `CreateGameEndpoint` |
| `PUT` | `/games/{id}` | Update | `UpdateGameEndpoint` |
| `DELETE` | `/games/{id}` | Delete | `DeleteGameEndpoint` |

**Update pattern (PUT):**
```csharp
var game = await dbContext.Games.FindAsync(id);  // 1. Find the existing game (async)
if (game is null) return Results.NotFound();

game.Name = gameDto.Name;                        // 2. Apply changes to the tracked entity
game.Price = gameDto.Price;
// ...

await dbContext.SaveChangesAsync();              // 3. EF Core detects changes and issues UPDATE SQL
return Results.NoContent();                      // 4. Return 204 — success, nothing to return
```

EF Core "tracks" the entity retrieved by `FindAsync()`. When you mutate its properties and call `SaveChangesAsync()`, EF Core automatically generates the SQL `UPDATE` statement. All endpoint handlers are `async` so they free the thread while waiting for the database.

---
### HTTP Status Codes & Results

**What it is:**
HTTP status codes are standardized numbers included in every response that tell the client what happened. `Results` is an ASP.NET Core helper for building responses with the correct code.

**Why it's used:**
Clients (browsers, mobile apps, other APIs) rely on status codes to understand the outcome of a request without parsing the body.

**How it fits:**

```csharp
Results.Ok(data)              // 200 — Success, returns data
Results.NotFound()            // 404 — Resource doesn't exist
Results.NoContent()           // 204 — Success, nothing to return (used for updates/deletes)
Results.CreatedAtRoute(...)   // 201 — Resource created, includes its URL in the response
```

**Pattern used in GET by ID:**
```csharp
var game = await dbContext.Games.FindAsync(id);
return game is null ? Results.NotFound() : Results.Ok(new GameDetailsDto(...));
```

If the game doesn't exist, return 404. Otherwise return 200 with the data.

---
### Constants & Named Endpoints

**What it is:**
Instead of hardcoding a string like `"GetGame"` in multiple places, you define it once as a constant. Named endpoints give an endpoint an identifier that can be referenced elsewhere.

**Why it's used:**
If you hardcode a string in two places and later rename it, you have to find and update every occurrence. A constant means you change it once.

**How it fits — `EndpointName.cs`:**
```csharp
public class EndpointNames
{
    public const string GetGame = nameof(GetGame);
}
```

`nameof(GetGame)` returns the string `"GetGame"` at compile time — if you rename the constant, the string updates automatically.

The endpoint is named when it's defined:
```csharp
app.MapGet("/{id}", ...).WithName(EndpointNames.GetGame);
```

And referenced when creating a resource:
```csharp
Results.CreatedAtRoute(EndpointNames.GetGame, new { id = game.Id }, dto)
```

---
### CreatedAtRoute

**What it is:**
`Results.CreatedAtRoute()` returns a `201 Created` HTTP response that includes a `Location` header pointing to the URL of the newly created resource.

**Why it's used:**
REST convention says when you create a resource, you should tell the client where to find it. The `Location` header lets the client immediately navigate to the new resource without having to construct the URL themselves.

**How it fits — `CreateGameEndpoint`:**
```csharp
dbContext.Add(game);
await dbContext.SaveChangesAsync();

return Results.CreatedAtRoute(
    EndpointNames.GetGame,     // Name of the endpoint to generate the URL from
    new { id = game.Id },      // Route parameters to fill in the URL template
    new GameDetailsDto(...)    // The response body
);
```

The response will have:
- Status: `201 Created`
- Header: `Location: /games/{new-game-id}`
- Body: The created game's details

---
### Problem Details & Standardized Error Responses

**What it is:**
`AddProblemDetails()` registers ASP.NET Core's built-in support for the [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457) standard — a consistent JSON format for HTTP error responses.

Without it, a `404` returns an empty body. With it, the response is a structured object a client can reliably parse:
```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404
}
```

**Why it's used:**
API clients shouldn't have to guess what an error response looks like. Problem Details gives every error a consistent shape — status code, human-readable title, optional detail message. It's a recognized standard, so clients and tooling already understand it.

**How it fits — `Program.cs`:**
```csharp
// Builder phase — register the service
builder.Services.AddProblemDetails();

// App phase — wire up the middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler(); // catches unhandled exceptions, formats them as Problem Details
}

app.UseStatusCodePages(); // turns empty error responses (404, 405, etc.) into Problem Details
```

**The three pieces together:**

| Call | What it does |
|---|---|
| `AddProblemDetails()` | Registers the service and formatter — nothing works without this |
| `UseExceptionHandler()` | Catches unhandled exceptions and converts them to a `500` Problem Details response |
| `UseStatusCodePages()` | Intercepts responses with an error status code and no body, and adds a Problem Details body |

**Why `UseExceptionHandler()` is wrapped in `!IsDevelopment()`:**
In development you want the full exception — stack trace, message, and all. `UseExceptionHandler()` would swallow that and return a generic `500`. Wrapping it in the environment check means dev gets the raw error and production gets the clean, safe response.

---
### Global Error Handler (`IExceptionHandler`)

**What it is:**
`IExceptionHandler` is an interface you implement to take full control of what happens when an unhandled exception reaches the pipeline. You register the class with `AddExceptionHandler<T>()` and it plugs into `UseExceptionHandler()` automatically.

**Why it's used:**
`UseExceptionHandler()` alone with `AddProblemDetails()` returns a generic `500` — no log, no trace ID, no customization. A custom `IExceptionHandler` lets you log the exception, attach a trace ID to the response, and return exactly the Problem Details shape you want.

**How it fits — `GlobalErrorHandler.cs`:**
```csharp
public class GlobalErrorHandler(ILogger<GlobalErrorHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = Activity.Current?.TraceId;

        logger.LogError(
            exception,
            "Could not process a request on machine {Machine}. TraceId {TraceId}",
            Environment.MachineName,
            traceId
        );

        await Results
            .Problem(
                title: "An error occurred while processing your request.",
                statusCode: StatusCodes.Status500InternalServerError,
                extensions: new Dictionary<string, object?> { { "traceId", traceId.ToString() } }
            )
            .ExecuteAsync(httpContext);

        return true;
    }
}
```

**Breaking it down line by line:**

`Activity.Current?.TraceId` — reads the current distributed trace ID. ASP.NET Core automatically starts a trace for every request. The `?.` means it returns `null` if there's no active trace, instead of throwing.

`logger.LogError(exception, ...)` — logs the full exception (message + stack trace) at the `Error` level. The `exception` is passed as the first argument so the logger captures it properly — not just as a string, but as a structured object.

`{Machine}` and `{TraceId}` — named placeholders in the log message. The values are stored as searchable properties in structured log output, not just embedded in a string.

`Results.Problem(...)` — builds a Problem Details response manually, letting you set the title, status code, and any extra fields (`extensions`). Here the `traceId` is added so the client can report it when something goes wrong.

`extensions` — an optional dictionary for extra fields in the Problem Details body. Here it injects the trace ID so the client can include it in a bug report:
```json
{
  "title": "An error occurred while processing your request.",
  "status": 500,
  "traceId": "4bf92f3577b34da6a3ce929d0e0e4736"
}
```

`.ExecuteAsync(httpContext)` — writes the response to the HTTP connection. `Results.Problem(...)` builds the response object but doesn't send it — you have to call `ExecuteAsync` manually inside `IExceptionHandler`.

`return true` — tells the framework this handler handled the exception. If you return `false`, the framework keeps looking for another handler.

**Registering it — `Program.cs`:**
```csharp
// builder phase
builder.Services.AddExceptionHandler<GlobalErrorHandler>();
builder.Services.AddProblemDetails();

// app phase
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}
```

`AddExceptionHandler<GlobalErrorHandler>()` registers the handler in the DI container. `AddProblemDetails()` is still needed — it sets up the Problem Details formatter that `Results.Problem()` uses. `UseExceptionHandler()` activates the pipeline middleware that invokes registered handlers when an exception is thrown.

**`IExceptionHandler` vs `UseExceptionHandler()` alone:**

| | `UseExceptionHandler()` alone | With `IExceptionHandler` |
|---|---|---|
| Logs the exception | No | Yes — you control the log |
| Includes trace ID | No | Yes — you add it to extensions |
| Custom response shape | No | Yes — full control via `Results.Problem()` |
| Multiple handlers | No | Yes — register many, each can handle or pass |

---
### OpenAPI

**What it is:**
OpenAPI (formerly Swagger) is a standard specification for describing HTTP APIs in a machine-readable format (JSON or YAML). It documents every endpoint — its path, method, parameters, request body shape, and response shapes — in a structured way that tools can consume.

**Why it's used:**
Without OpenAPI, API consumers have to read source code or hand-written docs to know what endpoints exist and what they accept. With OpenAPI, you get:
- A live, auto-generated spec that stays in sync with the code
- Free UI explorers (Swagger UI, Scalar) that let you call the API from a browser
- Client code generation — tools can produce typed API clients from the spec

**How it fits:**
ASP.NET Core 10 ships built-in OpenAPI support via `Microsoft.AspNetCore.OpenApi`. No third-party library needed.

Two steps to enable it:

```csharp
// Builder phase — register the OpenAPI document generator
builder.Services.AddOpenApi();

// App phase — expose the spec as a JSON endpoint (dev only)
if (app.Environment.IsDevelopment())
    app.MapOpenApi();
```

`MapOpenApi()` adds a route at `/openapi/v1.json` that serves the generated spec. It's gated behind `IsDevelopment()` — the spec describes your internal API surface in detail, so you don't want it publicly accessible in production.

**The generated spec:**
Navigating to `http://localhost:5078/openapi/v1.json` in development returns a JSON document describing every registered endpoint. Any OpenAPI-compatible tool (Swagger UI, Scalar, Postman, Insomnia) can import this URL and give you a full interactive API explorer.

**NuGet packages required:**

| Package | Purpose |
|---------|---------|
| `Microsoft.AspNetCore.OpenApi` | Core OpenAPI generation and `AddOpenApi()` / `MapOpenApi()` |
| `Microsoft.Extensions.ApiDescription.Server` | Build-time spec generation (for CI/CD and client generation) |

---
### Pagination

**What it is:**
Pagination splits a large list of results into smaller pages, returning a fixed number of records at a time rather than the entire dataset. The client requests a specific page number and page size; the server returns that slice of data along with the total number of pages.

**Why it's used:**
Returning every row in a table in a single response is expensive — it wastes bandwidth, slows the client down, and puts unnecessary load on the database. Pagination makes large lists manageable at every layer.

**How it fits — `GetGamesEndpoint`:**

```csharp
async (GoGameShopContext dbContext, [AsParameters] GetGamesDto request) =>
{
    var skipCount = (request.PageNumber - 1) * request.PageSize;

    var filteredGames = dbContext.Games
        .Where(game =>
            string.IsNullOrWhiteSpace(request.Name)
            || EF.Functions.Like(game.Name, $"%{request.Name}%"));

    var gamesOnPage = await filteredGames
        .OrderBy(game => game.Name)
        .Skip(skipCount)
        .Take(request.PageSize)
        .Include(game => game.Genre)
        .Include(game => game.Rating)
        .Select(game => new GameSummaryDto(...))
        .AsNoTracking()
        .ToListAsync();

    var totalGames = await filteredGames.CountAsync();
    var totalPages = (int)Math.Ceiling(totalGames / (double)request.PageSize);

    return new GamesPageDto(totalPages, gamesOnPage);
}
```

Breaking it down:

- **`(request.PageNumber - 1) * request.PageSize`** — page 1 starts at offset 0, page 2 at offset `pageSize`, etc.
- **`.Skip(skipCount)`** — skips that many rows (translated to SQL `OFFSET`).
- **`.Take(request.PageSize)`** — takes only `pageSize` rows (translated to SQL `LIMIT`).
- **`.OrderBy(game => game.Name)`** — pagination without an `OrderBy` is non-deterministic; the database can return rows in any order, so the same record can appear on two different pages. Always sort when paginating.
- **`filteredGames.CountAsync()`** — runs a separate `SELECT COUNT(*) FROM Games WHERE ...` scoped to the same filter, so total pages reflect only matching records.
- **`Math.Ceiling(totalGames / (double)request.PageSize)`** — divides total rows by page size, rounding up so the last partial page is still counted. The cast to `double` is necessary — integer division would truncate (e.g. `11 / 5 = 2` instead of `3`).

The response is wrapped in `GamesPageDto` so the client knows how many pages exist:
```csharp
public record GamesPageDto(int TotalPages, IEnumerable<GameSummaryDto> Games);
```

The client sends page requests as query strings:
```
GET /games?pageNumber=1&pageSize=10
GET /games?pageNumber=1&pageSize=10&Name=metal
```

---
### AsParameters — Binding Query Strings to Records

**What it is:**
`[AsParameters]` is a Minimal API attribute that tells ASP.NET Core to bind a complex object from the request instead of treating it as a JSON body. Query string values, route values, and headers are mapped by name to the record's properties.

**Why it's used:**
Without `[AsParameters]`, ASP.NET Core assumes any object parameter in a Minimal API handler comes from the JSON request body. For query string parameters you'd normally have to list each one individually:
```csharp
async (GoGameShopContext dbContext, int pageNumber = 1, int pageSize = 5) => { ... }
```
`[AsParameters]` lets you group them into a record instead, keeping the handler signature clean.

**How it fits:**
```csharp
public record GetGamesDto(int PageNumber = 1, int PageSize = 5, string? Name = null);

app.MapGet("/", async (GoGameShopContext dbContext, [AsParameters] GetGamesDto request) =>
{
    // request.PageNumber, request.PageSize, and request.Name come from the query string
});
```

The default values (`PageNumber = 1, PageSize = 5`) are used when the client omits those query parameters — so `GET /games` works the same as `GET /games?pageNumber=1&pageSize=5`. `Name` defaults to `null`, meaning no filter is applied when the client omits it.

---
### Search Filtering

**What it is:**
Search filtering lets clients narrow results by providing a query parameter. Instead of returning every game, the endpoint returns only games whose names contain the search term.

**Why it's used:**
Pagination reduces *how many* records come back at once. Filtering reduces *which* records come back at all. Together they make browsing a large catalog practical.

**How it fits — `GetGamesEndpoint`:**

The filter is optional — if the client omits `Name`, all games are returned:
```csharp
public record GetGamesDto(int PageNumber = 1, int PageSize = 5, string? Name = null);
```

The filter is applied to a base query variable before pagination is layered on:
```csharp
var filteredGames = dbContext.Games
    .Where(game =>
        string.IsNullOrWhiteSpace(request.Name)
        || EF.Functions.Like(game.Name, $"%{request.Name}%"));

var gamesOnPage = await filteredGames
    .OrderBy(game => game.Name)
    .Skip(skipCount)
    .Take(request.PageSize)
    // ...
    .ToListAsync();

var totalGames = await filteredGames.CountAsync();
```

**`EF.Functions.Like`:**

`EF.Functions.Like(column, pattern)` maps to SQL's `LIKE` operator. The `%` wildcard matches zero or more characters anywhere in the string:

```csharp
EF.Functions.Like(game.Name, $"%{request.Name}%")
// SQL: Name LIKE '%metal%'  → matches any name containing "metal"
```

`EF.Functions.Like` is preferred over `.Contains()` because it translates directly to a SQL `LIKE` clause, which SQLite evaluates case-insensitively without any extra configuration.

**Why the filter is a separate variable:**

`filteredGames` is an `IQueryable<Game>` — it's a query *description*, not a result. EF Core doesn't hit the database until `.ToListAsync()` or `.CountAsync()` is called. Assigning it to a variable lets both the page fetch and the count query share the same filter without repeating it:

```csharp
var gamesOnPage = await filteredGames.Skip(...).Take(...).ToListAsync(); // one SQL query
var totalGames  = await filteredGames.CountAsync();                       // another SQL query
// Both are automatically scoped to the same WHERE clause
```

**Why `string.IsNullOrWhiteSpace` is used as the guard:**

The `||` short-circuits — if `request.Name` is `null` or whitespace, EF Core skips the `Like` predicate entirely and the `Where` clause adds no filter. This is the correct pattern for optional filters: omit the filter when the value is absent, rather than filtering for an empty string (which would return no results).

```
GET /games                              → all games, paginated
GET /games?Name=metal                   → games whose name contains "metal"
GET /games?pageNumber=2&pageSize=5&Name=gear  → page 2 of results matching "gear"
```

---
### File Uploads — IFormFile

**What it is:**
`IFormFile` is ASP.NET Core's type for a file submitted in a `multipart/form-data` HTTP request. It represents one uploaded file, with properties for the file name, content type, size, and a stream to read its bytes.

**Why it's used:**
File uploads arrive as binary data in a multipart form body — not JSON. `IFormFile` wraps the raw stream with typed metadata so you can validate and save the file without manually parsing the HTTP body.

**How it fits — `FileUploader.UploadFileAsync`:**

```csharp
public async Task<FileUploadResult> UploadFileAsync(IFormFile file, string folder)
{
    // 1. Validate presence
    if (file == null || file.Length == 0)
        return new FileUploadResult { IsSucess = false, ErrorMessage = "File not found" };

    // 2. Validate size (< 10 MB)
    if (file.Length > 10 * 1024 * 1024)
        return new FileUploadResult { IsSucess = false, ErrorMessage = "File size is too large" };

    // 3. Validate extension
    string[] permittedExtensions = [".jpg", ".jpeg", ".png"];
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (string.IsNullOrEmpty(ext) || !permittedExtensions.Contains(ext))
        return new FileUploadResult { IsSucess = false, ErrorMessage = "Unsupported file type" };

    // 4. Generate a safe file name and save to disk
    var safeFileName = $"{Guid.NewGuid()}{ext}";
    var fullPath = Path.Combine(environment.WebRootPath, folder, safeFileName);
    using var stream = new FileStream(fullPath, FileMode.Create);
    await file.CopyToAsync(stream);

    // 5. Build and return the public URL
    var fileUrl = $"{httpContext.Request.Scheme}://{httpContext.Request.Host}/{folder}/{safeFileName}";
    return new FileUploadResult { IsSucess = true, FileUrl = fileUrl };
}
```

**Key concepts:**

`file.Length` — size in bytes. `10 * 1024 * 1024` is 10 MB written as byte arithmetic rather than a raw number, making the intent immediately readable.

`Path.GetExtension(file.FileName).ToLowerInvariant()` — extracts the extension from the original client file name. `.ToLowerInvariant()` normalizes it so `.JPG` and `.jpg` are treated the same.

**Why not trust the extension alone?** The extension is just a string — any client can rename `malware.exe` to `file.jpg`. In production, you'd also validate the file's actual content (magic bytes / MIME sniffing). Extension checking is the first layer.

`Guid.NewGuid()` — generates a globally unique ID used as the file name on disk. This prevents two uploads with the same original name from overwriting each other and prevents clients from guessing other users' file paths.

`environment.WebRootPath` — the absolute path to the `wwwroot/` folder. Files placed here are accessible as static files when `UseStaticFiles()` is active. `IWebHostEnvironment` is injected via constructor DI.

`file.CopyToAsync(stream)` — streams the uploaded bytes directly into a `FileStream`. Using the `Async` variant means the thread is free while bytes are being written to disk — no blocking.

**`FileUploadResult` — returning structured results from a service:**

Rather than throwing exceptions or returning raw strings, the `FileUploader` returns a result object that carries success state and either the file URL or an error message:

```csharp
public class FileUploadResult
{
    public bool IsSucess { get; set; }
    public string? FileUrl { get; set; }
    public string? ErrorMessage { get; set; }
}
```

This pattern keeps the caller in control — it can inspect the result and decide how to respond (return a `400`, log the error, etc.) without needing to catch exceptions for expected failure cases.

**Registering the service — `Program.cs`:**
```csharp
builder.Services.AddHttpContextAccessor();   // required so FileUploader can access the request
builder.Services.AddSingleton<FileUploader>(); // one shared instance for the whole app
```

`AddSingleton` is appropriate because `FileUploader` has no per-request state — it's a stateless service that acts on the data passed to it. The `IHttpContextAccessor` it holds is thread-safe because `AsyncLocal` is context-bound per request.

---

## Async Programming

### Async/Await

**What it is:**
`async` and `await` are C# keywords that let you write non-blocking code that reads like synchronous code. An `async` method can pause at an `await` expression and release the thread while it waits for a result — without blocking.

**Why it's used:**
Web servers handle many requests at once. If every request blocks a thread while waiting for a database query, you run out of threads under load. `async/await` lets the thread do other work while the database is busy — making the application more scalable.

**How it fits:**
Every endpoint in the project is now async:
```csharp
// Before (sync — blocks the thread while the DB query runs):
app.MapGet("/", (GoGameShopContext dbContext) =>
    dbContext.Games.ToList());

// After (async — thread is free during the DB query):
app.MapGet("/", async (GoGameShopContext dbContext) =>
    await dbContext.Games.ToListAsync());
```

Rules:
- A method must be marked `async` to use `await` inside it.
- `async` methods that return something return `Task<T>`. Methods that return nothing return `Task` (not `void`).
- `await` unwraps the result from the `Task` — so `await dbContext.Games.ToListAsync()` gives you a `List<Game>`, not a `Task<List<Game>>`.
- You can only `await` at the top level of a .NET 10 application (like `Program.cs`) because .NET now supports top-level async entry points.

---
### The Task Type

**What it is:**
`Task` is C#'s representation of an ongoing asynchronous operation. Think of it as a promise: "this will eventually produce a result (or finish) in the future."

**Why it's used:**
When you mark a method `async`, it no longer runs to completion before returning — it returns a `Task` immediately. The caller can `await` that `Task` to get the result when it's ready.

**Three forms:**

| Type | Meaning |
|------|---------|
| `Task` | Async operation with no return value |
| `Task<T>` | Async operation that returns a value of type `T` |
| `ValueTask<T>` | Lightweight version of `Task<T>` (see section 28) |

**How it fits:**
```csharp
// Returns Task — no value, just "done when done"
public static async Task InitializeDbAsync(this WebApplication app)
{
    await app.MigrateDbAsync();
    await app.SeedDbAsync();
}

// Returns Task<T> — produces a List<Game> when done
public async Task<List<Game>> GetGamesAsync()
{
    return await dbContext.Games.ToListAsync();
}
```

`Task` itself has useful properties and methods:
- `task.IsCompleted` — true if the operation is done
- `task.Result` — the result (only use after awaiting, otherwise it blocks)
- `await Task.WhenAll(task1, task2)` — wait for multiple tasks to finish in parallel
- `await Task.WhenAny(task1, task2)` — wait for whichever finishes first

---
### ValueTask & .AsTask()

**What `ValueTask` is:**
`ValueTask<T>` is a lightweight alternative to `Task<T>` that avoids heap allocation when a result is available immediately (synchronously). EF Core's `FindAsync()` returns `ValueTask<T>` for this reason — if the entity is already in the context's cache, no real async work is needed.

```csharp
// FindAsync returns ValueTask<Game?>, not Task<Game?>
var game = await dbContext.Games.FindAsync(id);
```

You can `await` a `ValueTask` just like a `Task` — in most cases you don't need to think about the difference.

**What `.AsTask()` does:**
`.AsTask()` converts a `ValueTask` or `ValueTask<T>` into a regular `Task` or `Task<T>`. This is needed when you want to pass the operation to something that only accepts `Task`, such as `Task.WhenAll()` or `ContinueWith()`.

```csharp
ValueTask<Game?> valueTask = dbContext.Games.FindAsync(id);

// Convert to Task so you can use Task.WhenAll or ContinueWith:
Task<Game?> task = valueTask.AsTask();

await Task.WhenAll(task, someOtherTask);
```

**Important:** Only call `.AsTask()` once per `ValueTask`. Unlike `Task`, a `ValueTask` must not be awaited or converted more than once — doing so is undefined behavior.

**In practice:** You rarely need `.AsTask()` in everyday code. It becomes relevant when:
- Combining multiple async operations with `Task.WhenAll`
- Using continuation-style code with `ContinueWith`
- Storing or passing around an async operation as a `Task`

---
### ContinueWith

**What it is:**
`ContinueWith()` is a method on `Task` that schedules a callback to run when the task completes. It's the older, explicit way to chain async operations — it predates `async/await` syntax.

**Why it's useful to know:**
You'll encounter `ContinueWith` in older codebases and in scenarios that need fine-grained control over continuation behavior (e.g., running only on success, only on failure, or on a specific thread).

**How it works:**
```csharp
Task<Game?> findTask = dbContext.Games.FindAsync(id).AsTask();

// Schedule work to run after findTask finishes:
Task<IResult> result = findTask.ContinueWith(completedTask =>
{
    var game = completedTask.Result;  // Safe here — task is guaranteed complete
    return game is null ? Results.NotFound() : Results.Ok(game);
});
```

The callback receives the completed `Task` as its argument, so you can inspect `.Result`, `.IsFaulted`, or `.Exception`.

**`ContinueWith` vs `async/await`:**

| | `ContinueWith` | `async/await` |
|--|----------------|---------------|
| Readability | Callback nesting, harder to follow | Reads like sequential code |
| Error handling | Must check `.IsFaulted` manually | `try/catch` works normally |
| Context | Explicit control via `TaskScheduler` | Captures context automatically |
| Use case | Legacy code, advanced scheduling | Everything else |

**In this project:** All endpoints use `async/await` — `ContinueWith` is not used directly. But understanding it helps when reading framework source code or older libraries, and explains *why* `async/await` was designed the way it was.

---

*These notes grow as the project grows. Each new concept implemented will be documented here.*