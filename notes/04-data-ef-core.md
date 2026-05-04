## Data and EF Core

### Models & Entities

**The problem:**
A relational database thinks in tables, columns, and rows. Application code thinks in objects with methods and references. Without a bridge, every read and write is a manual translation between the two — tedious to write and easy to get inconsistent.

**What it does:**
A **model** (also called an **entity**) is a C# class that represents a real-world thing your app works with — `Game`, `Genre`, `Rating`. In EF Core, each model maps to a database table, so you work with objects in code and EF Core handles the SQL.

**In code — `Game.cs`:**
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

**The problem:**
Hand-writing SQL for every query is repetitive and error-prone, parameter-binding bugs become SQL injection holes, and the resulting strings are invisible to the compiler — a typo in a column name fails at runtime, not build time. There also needs to be something to evolve the schema as the C# models change, instead of writing migration scripts by hand.

**What it does:**
EF Core is an **Object-Relational Mapper (ORM)**. It translates LINQ over C# objects into SQL, tracks changes so it knows what to save, manages relationships, and generates schema migrations. A query like:

```csharp
dbContext.Games.Where(g => g.Price < 30).ToList()
```

becomes the equivalent `SELECT * FROM Games WHERE Price < 30` automatically.

**In code:**
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

**The problem:**
Querying a database needs a connection, change tracking, and a registry of which classes map to which tables — all of it scoped to a single unit of work. Spreading that across the app would mean every query reinventing connection handling and change detection.

**What it does:**
`DbContext` is the central class in EF Core — a session with the database. It owns the connection, exposes each table as a `DbSet<T>`, tracks changes to loaded entities, and coordinates saving them in one transaction via `SaveChanges()`.

**In code — `GoGameShopContext.cs`:**
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

**The problem:**
The C# model evolves continuously — fields get added, tables get renamed, indexes get introduced. The database has to track those changes too, but applying them ad-hoc means dev/staging/prod drift apart, and rolling back a bad change becomes guesswork.

**What it does:**
A **migration** is a versioned, code-form snapshot of a schema change — generated automatically when models change and applied in order to bring any database up to date. Each migration is a checked-in C# file, so the schema's history lives next to the code that drives it and the same migrations replay identically across every environment.

**In code:**
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

**The problem:**
A fresh database is empty. Some data — reference lookups like genres and age ratings, an initial admin user — has to be there for the app to function at all, and asking every developer to insert it manually after each `database update` is a nonstarter.

**What it does:**
**Seeding** pre-populates the database with required initial data when the app starts, gated by an existence check so the same code is safe to run on every startup. The seed lives in version control alongside the schema, so any new environment is one boot away from a usable state.

**In code — `DataExtensions.cs`:**
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

**The problem:**
By default, EF Core tracks every entity it loads — keeping a snapshot in memory so it can compute what changed when you call `SaveChanges()`. That bookkeeping has memory and CPU cost, and for pure read endpoints (the ones that hit the database hardest) it's wasted overhead.

**What it does:**
`AsNoTracking()` opts a query out of change tracking. The entities are returned but never registered with the context, so reads are faster and lighter. Use it on any query whose results will never be saved back.

**In code:**
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

**The problem:**
The traditional EF Core delete pattern is: load the entity into memory → call `Remove()` → call `SaveChanges()`. That's two round trips to the database for an operation that should need only one — wasteful when you don't actually need the entity, you just want it gone.

**What it does:**
`ExecuteDelete()` translates directly into a single SQL `DELETE` statement without loading the entity first. One round trip, no tracking, no allocation for an object you were going to discard anyway.

**In code — `DeleteGameEndpoint`:**
```csharp
await dbContext.Games
    .Where(game => game.Id == id)
    .ExecuteDeleteAsync();
```

This generates: `DELETE FROM Games WHERE Id = @id`

No entity is loaded into memory — it's a direct, efficient delete. `ExecuteDeleteAsync()` is the async version, returning a `Task` so the thread is free while the database processes the statement.

---
### Include (Eager Loading)

**The problem:**
Entities reference each other (a `Game` has a `Genre` and a `Rating`), but EF Core won't load those references by default — accessing them returns `null`. Loading each one with a separate query per row produces the classic N+1 problem: one query becomes hundreds.

**What it does:**
`Include()` tells EF Core to load a navigation property in the same query as the parent, using a SQL `JOIN`. One round trip, all the related data populated. There are three loading strategies in EF Core — eager (`Include`), explicit (separate query on demand), and lazy (auto-load on access, requires proxies, not used here).

**In code — `GET /games`:**
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

---

