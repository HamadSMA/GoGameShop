## Language and Patterns

### Extension Methods

**The problem:**
You often want to add behavior to a type you don't own (`WebApplication`, `string`, `IEnumerable<T>`) — but you can't edit its source. Wrapping it in a helper class works, but the call site reads awkwardly: `Helpers.DoThing(target)` instead of `target.DoThing()`.

**What it does:**
An **extension method** is a static method that *appears* to live on the target type. The `this` keyword on the first parameter tells the compiler to allow `target.Method()` syntax — same dispatch as a real method, no inheritance or modification of the original type required.

**In code:**
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

**The problem:**
When a class creates its own dependencies (`new DbContext(...)`, `new HttpClient()`), it owns their lifecycle, locks itself to specific implementations, and becomes nearly impossible to swap out for tests. The codebase ends up with hidden coupling and duplicated `using` blocks for resource cleanup.

**What it does:**
**Dependency Injection (DI)** flips construction: objects declare what they need as constructor or method parameters, and a container provides them at runtime. ASP.NET Core's built-in container manages instance lifetimes (Singleton / Scoped / Transient) and disposes of disposables automatically — you just declare the dependency.

**In code:**
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

**The problem:**
Most DTOs are pure data — properties, value equality, a `ToString()` for debugging, and nothing else. Writing them as classes means dozens of lines of boilerplate (constructor, properties, `Equals`, `GetHashCode`, `ToString`) per type, and the noise hides what's actually distinctive about each one.

**What it does:**
A `record` is a C# type designed for immutable data. The compiler auto-generates the constructor, value-based equality, `GetHashCode`, and `ToString` from the property list — turning a 30-line class into a one-liner that still has the same behavior.

**In code:**
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

**The problem:**
Wrapping the entire body of a method in nested `if` blocks pushes the real work two or three indents deep, and intersperses the happy path with error handling. It also wastes work — if the input is invalid, you've already opened a file or hit the database before noticing.

**What it does:**
**Fail-fast validation** (also called *guard clauses*) checks preconditions at the top of the method and returns immediately on the first failure. The happy path stays at the outermost indent and runs only after every check has passed — and no expensive work happens for bad input.

**In code:**
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

**The problem:**
Layered architectures (`Controllers/`, `Services/`, `Repositories/`) scatter the code for one feature across many folders. Adding "Create Game" means editing four files in four different layers, and a teammate who wants to understand the feature has to chase references across all of them.

**What it does:**
**Vertical slicing** organizes code by **feature** instead of by technical layer. Each folder owns everything one feature needs — endpoint, DTOs, validation, handlers — so adding a feature is adding a folder, and reading a feature is reading one folder.

**In code:**
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

**The problem:**
Filtering, projecting, sorting, and aggregating collections with bare `foreach` loops produces a lot of imperative bookkeeping for a simple intent. The loop body buries *what* you want under *how* to compute it, and there's no way for an ORM to translate a `foreach` into SQL.

**What it does:**
**LINQ** is a set of C# methods (`Where`, `Select`, `OrderBy`, `Sum`, …) for working with collections in a declarative, chainable style. The same methods work over in-memory collections (`IEnumerable<T>`) and over database queries (`IQueryable<T>`) — EF Core translates the second into SQL automatically.

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
The .NET standard library ships with utility methods on strings, collections, arrays, and more. These are the ones you'll reach for constantly — keep them visible so you don't reinvent them or import a library for what's already in the BCL.

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

