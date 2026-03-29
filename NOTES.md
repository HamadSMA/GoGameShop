# GoGameShop — Learning Notes

> [!NOTE]
> These notes are AI-assisted and personally customized as I manually write code in this project. They serve as my reference and review material.

---

## Table of Contents

1. [ASP.NET Core & Minimal APIs](#1-aspnet-core--minimal-apis)
2. [Program.cs — The Entry Point](#2-programcs--the-entry-point)
3. [The .csproj File — Project Configuration](#3-the-csproj-file--project-configuration)
4. [appsettings.json — App Configuration](#4-appsettingsjson--app-configuration)
5. [Global Usings](#5-global-usings)
6. [Models & Entities](#6-models--entities)
7. [Entity Framework Core (EF Core)](#7-entity-framework-core-ef-core)
8. [DbContext](#8-dbcontext)
9. [Database Migrations](#9-database-migrations)
10. [Database Seeding](#10-database-seeding)
11. [Extension Methods](#11-extension-methods)
12. [Dependency Injection](#12-dependency-injection)
13. [Route Groups](#13-route-groups)
14. [DTOs (Data Transfer Objects)](#14-dtos-data-transfer-objects)
15. [C# Records](#15-c-records)
16. [Data Annotations & Validation](#16-data-annotations--validation)
17. [CRUD Endpoints](#17-crud-endpoints)
18. [HTTP Status Codes & Results](#18-http-status-codes--results)
19. [AsNoTracking](#19-asnotracking)
20. [ExecuteDelete](#20-executedelete)
21. [Include (Eager Loading)](#21-include-eager-loading)
22. [Vertical Slice Architecture](#22-vertical-slice-architecture)
23. [Constants & Named Endpoints](#23-constants--named-endpoints)
24. [CreatedAtRoute](#24-createdatroute)
25. [launchSettings.json](#25-launchsettingsjson)
26. [Async/Await](#26-asyncawait)
27. [The Task Type](#27-the-task-type)
28. [ValueTask & .AsTask()](#28-valuetask--astask)
29. [ContinueWith](#29-continuewith)

---

## 1. ASP.NET Core & Minimal APIs
*March 17, 2026*

**What it is:**
ASP.NET Core is Microsoft's framework for building web applications and APIs with C#. A **Minimal API** is a lightweight way to define HTTP endpoints directly in code — without needing controllers, classes, or a lot of boilerplate.

**Why it's used:**
Traditional ASP.NET used "controllers" — classes with many methods. Minimal APIs skip that overhead and let you write endpoints directly, which is simpler and faster for APIs.

**How it fits:**
Every endpoint in this project (`MapGet`, `MapPost`, etc.) is a Minimal API. Instead of a `GamesController` class, there is a `MapGames()` method that registers all game-related routes.

---

## 2. Program.cs — The Entry Point
*March 17, 2026*

**What it is:**
`Program.cs` is the starting point of a .NET application. Every .NET app has one. It's where you configure services (things the app needs) and the request pipeline (how requests are handled).

**Why it's used:**
.NET needs a single place to wire everything together — the database, routing, middleware, and startup logic all get registered here.

**How it fits:**
```csharp
var builder = WebApplication.CreateBuilder(args);  // Creates the app builder
var connectionString = builder.Configuration.GetConnectionString("GoGameShop");

builder.Services.AddSqlite<GoGameShopContext>(connectionString);  // Register the database
builder.Services.AddValidation();                                 // Register validation

var app = builder.Build();                                        // Build the app

app.MapGames();                     // Register /games endpoints
app.MapGetGenres();                 // Register /genres endpoint
app.MapGetRatings();                // Register /ratings endpoint
await app.InitializeDbAsync();      // Run migrations & seed data (async)

app.Run();               // Start the server
```

The two phases are:
- **Builder phase** (`builder.Services.*`): Register services into the DI container
- **App phase** (`app.*`): Configure the pipeline and run

---

## 3. The .csproj File — Project Configuration
*March 17, 2026*

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

## 4. appsettings.json — App Configuration
*March 17, 2026*

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

`appsettings.Development.json` overrides settings specifically for the Development environment, keeping sensitive production settings separate.

---

## 5. Global Usings
*March 21, 2026*

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

## 6. Models & Entities
*March 18, 2026*

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

## 7. Entity Framework Core (EF Core)
*March 22, 2026*

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

---

## 8. DbContext
*March 22, 2026*

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

## 9. Database Migrations
*March 24, 2026*

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

## 10. Database Seeding
*March 27, 2026*

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

## 11. Extension Methods
*March 25, 2026*

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
- `app.InitializeDb()` — runs migrations and seeds
- `dbContext.Games.AsNoTracking()` — disables tracking (built into EF Core)

---

## 12. Dependency Injection
*March 22, 2026*

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

## 13. Route Groups
*March 21, 2026*

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

## 14. DTOs (Data Transfer Objects)
*March 20, 2026*

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

## 15. C# Records
*March 20, 2026*

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

## 16. Data Annotations & Validation
*March 21, 2026*

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

## 17. CRUD Endpoints
*March 21, 2026*

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

## 18. HTTP Status Codes & Results
*March 21, 2026*

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
var game = dbContext.Games.Find(id);
return game is null ? Results.NotFound() : Results.Ok(new GameDetailsDto(...));
```

If the game doesn't exist, return 404. Otherwise return 200 with the data.

---

## 19. AsNoTracking
*March 27, 2026*

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

## 20. ExecuteDelete
*March 27, 2026*

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

## 21. Include (Eager Loading)
*March 27, 2026*

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

## 22. Vertical Slice Architecture
*March 21, 2026*

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

## 23. Constants & Named Endpoints
*March 21, 2026*

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

## 24. CreatedAtRoute
*March 21, 2026*

**What it is:**
`Results.CreatedAtRoute()` returns a `201 Created` HTTP response that includes a `Location` header pointing to the URL of the newly created resource.

**Why it's used:**
REST convention says when you create a resource, you should tell the client where to find it. The `Location` header lets the client immediately navigate to the new resource without having to construct the URL themselves.

**How it fits — `CreateGameEndpoint`:**
```csharp
dbContext.Add(game);
dbContext.SaveChanges();

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

## 25. launchSettings.json
*March 17, 2026*

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

## 26. Async/Await
*March 29, 2026*

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

## 27. The Task Type
*March 29, 2026*

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

## 28. ValueTask & .AsTask()
*March 29, 2026*

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

## 29. ContinueWith
*March 29, 2026*

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
