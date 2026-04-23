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
### ThenInclude — Nested Eager Loading

**What it is:**
`ThenInclude` lets you load a navigation property on an already-included related entity. It chains off a previous `.Include()` call to go one level deeper.

**Why it's used:**
`.Include()` can only go one level deep. If you need to load a relationship on a relationship (e.g. basket items *and* each item's game), you chain `.ThenInclude()`.

**How it fits — `GetBasketEndpoints`:**
```csharp
dbContext.Baskets
    .Include(basket => basket.Items)         // load BasketItems for the basket
    .ThenInclude(item => item.Game)          // then load the Game for each BasketItem
    .FirstOrDefaultAsync(basket => basket.Id == userId)
```

Without `.ThenInclude(item => item.Game)`, `item.Game` would be `null` even though `item.GameId` has a value — EF Core won't load it automatically unless you ask.

**Computed properties on records:**
`BasketDto` uses `=>` to compute `TotalAmount` at access time:
```csharp
public record BasketDto(Guid CustomerId, IEnumerable<BasketItemDto> Items)
{
    public decimal TotalAmount => Items.Sum(item => item.Price * item.Quantity);
}
```

`=>` makes it a **property** — `System.Text.Json` serializes it into the response. If written as `= Items.Sum(...)` (a field), it would be silently excluded from the JSON output.

---
### [FromForm] — Binding Form Data

**What it is:**
`[FromForm]` is a parameter attribute in ASP.NET Core that tells the framework to bind the parameter from a `multipart/form-data` or `application/x-www-form-urlencoded` request body — not from JSON.

**Why it's used:**
By default, Minimal API assumes any object parameter comes from the JSON body. When an endpoint needs to accept a file upload alongside regular fields, the request must be `multipart/form-data` (because JSON cannot carry binary file data). `[FromForm]` switches the binding source so the DTO is populated from the form fields instead.

**How it fits:**
```csharp
app.MapPost("/", async ([FromForm] CreateGameDto gameDto, FileUploader fileUploader) =>
{
    // gameDto.Name, gameDto.Price, etc. come from form fields
    // gameDto.ImageFile comes from the file field (IFormFile)
});
```

The DTO is a `record` with scalar properties (bound from form fields) plus an `IFormFile?` property (bound from the file part):
```csharp
public record CreateGameDto(
    [Required][StringLength(50)] string Name,
    Guid GenreId,
    Guid RatingId,
    DateOnly ReleaseDate,
    [Range(1, 100)] decimal Price,
    [Required][StringLength(500)] string Description
)
{
    public IFormFile? ImageFile { get; set; }
}
```

The constructor parameters bind from named form fields. `ImageFile` is a mutable property rather than a constructor parameter because `IFormFile` requires separate binding — Minimal API's form binder handles it after the record is constructed.

**JSON vs form-data — when to use which:**

| Scenario | Binding source | Attribute |
|----------|----------------|-----------|
| Sending JSON | Body (default) | *(none)* |
| Sending form fields only | Form | `[FromForm]` |
| Sending form fields + file | Multipart form | `[FromForm]` |

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

