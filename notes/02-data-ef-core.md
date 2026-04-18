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

