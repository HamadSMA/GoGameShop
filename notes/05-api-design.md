## API Design

### Route Groups

**The problem:**
Endpoints that share a URL prefix (`/games`, `/games/{id}`, …) end up repeating that prefix on every registration. A typo in one place creates a silently broken route, and renaming the prefix means find-and-replacing every endpoint.

**What it does:**
A route group lets you apply a common URL prefix to a set of endpoints once. Each child endpoint declares only its relative path; the group prepends the prefix and any shared metadata (auth, filters) is inherited automatically.

**In code — `GamesEndpoints.cs`:**
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

**The problem:**
Returning the database entity straight to clients leaks internals — fields you didn't mean to expose, the wrong shape for the use case, and a coupling between the wire contract and the storage model that makes either one painful to change. Input shapes also rarely match output shapes (creating a game needs a name and price; reading one needs IDs and timestamps).

**What it does:**
A **DTO** is a purpose-built object for one direction of one operation — separate from the entity. It declares exactly which fields cross the API boundary, lets validation attach at the boundary instead of on the entity, and lets the wire contract evolve independently of the database schema.

**In code — three types used here:**

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

**The problem:**
Client input cannot be trusted — fields might be missing, strings might be too long, numbers might be out of range. Validating each one by hand inside every endpoint creates duplicated checks, inconsistent error messages, and the constant risk that some new endpoint forgets a check entirely.

**What it does:**
**Data annotations** are attributes (markers in square brackets) on DTO properties that declare validation rules. The framework enforces them automatically before the endpoint code runs — invalid requests get a `400 Bad Request` with a structured error body, and the handler never sees them.

**In code — `CreateGameDto`:**
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

**The problem:**
Every data-driven app needs the same four basic operations on its resources, but without a convention each team invents its own URL scheme — `/createGame`, `/games/new`, `/addGame` — and clients have to read docs to know which is which. Mixing verbs into URLs also makes caching, idempotency, and tooling support harder.

**What it does:**
CRUD — **Create, Read, Update, Delete** — maps cleanly onto HTTP verbs (`POST`, `GET`, `PUT`/`PATCH`, `DELETE`) with the resource as a noun in the path. The result is a predictable URL surface where any developer can guess the route and any tool can reason about it.

**In code:**

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

**The problem:**
A client needs to know whether a request succeeded, failed for a reason it can recover from, or failed because the server is broken — and it needs that signal *before* parsing the response body, because the body might be empty, missing, or in an unexpected format. Without standardized signals, every API would invent its own.

**What it does:**
HTTP status codes are the standardized signal: `2xx` worked, `4xx` is the client's fault, `5xx` is the server's. `Results.Ok()`, `Results.NotFound()`, `Results.NoContent()`, etc. are ASP.NET Core helpers that build responses with the correct status code and (when applicable) the body, so endpoints don't have to assemble the response by hand.

**In code:**

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

**The problem:**
Some endpoint identifiers (the route name used by `CreatedAtRoute` to build a `Location` header) need to match in at least two places — where the endpoint is named and where it's referenced. Hardcoding the same magic string in both spots means a typo or rename silently breaks redirect URLs at runtime, with no compiler warning.

**What it does:**
A **named endpoint** is an endpoint registered with a stable identifier (`.WithName(...)`), and a constants class holds those identifiers as `nameof`-backed `const string` fields. Every reference goes through the same constant, so a rename propagates and the compiler catches stale references.

**In code — `EndpointName.cs`:**
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

**The problem:**
After a `POST` creates a resource, the client usually needs the URL of the newly created thing — to fetch it, link to it, or redirect the user to it. Asking the client to build that URL itself ties it to the server's routing scheme and breaks the moment the URL pattern changes.

**What it does:**
`Results.CreatedAtRoute()` returns `201 Created` with a `Location` header pointing at the new resource's canonical URL, generated from a named endpoint plus route values. The server owns its URL scheme; the client just follows the `Location`.

**In code — `CreateGameEndpoint`:**
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

**The problem:**
By default, error responses are inconsistent — some return empty bodies, some return strings, some return ad-hoc JSON. Each client ends up writing its own special-case parser, and the API's error contract drifts as different endpoints invent different shapes.

**What it does:**
`AddProblemDetails()` enables ASP.NET Core's built-in support for the [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457) standard — a single consistent JSON shape for every HTTP error response. Clients and tooling already understand the format, so error handling becomes uniform across the whole API:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404
}
```

**In code — `Program.cs`:**
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

**The problem:**
Unhandled exceptions need to be caught somewhere — not catching them leaks stack traces to clients, and catching them per-endpoint duplicates code and inevitably misses cases. The default `UseExceptionHandler()` + Problem Details combo returns a generic `500` with no log, no trace ID, and no way to attach context for debugging.

**What it does:**
`IExceptionHandler` is an interface for taking full control of unhandled exceptions in one place. Registered via `AddExceptionHandler<T>()`, it plugs into `UseExceptionHandler()` and runs on every unhandled error — letting you log structured details, attach a trace ID, and shape the Problem Details response exactly the way you want.

**In code — `GlobalErrorHandler.cs`:**
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

**The problem:**
Without a machine-readable description of the API, every consumer has to read source code or hand-written docs to know which endpoints exist and what they accept — and those docs go stale the moment a route changes. There's also no way for tooling (UI explorers, typed client generators, contract tests) to plug in.

**What it does:**
OpenAPI (formerly Swagger) is a standard specification for describing HTTP APIs in JSON or YAML — every endpoint's path, method, parameters, and request/response shapes. Generated automatically from the running app, it stays in sync with the code and unlocks an ecosystem of tools: Swagger UI / Scalar for live exploration, code generators for typed clients, contract validators for CI.

**In code:**
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

**The problem:**
Returning every row in a table in a single response is expensive at every layer — the database scans the whole table, the network ships megabytes, and the client struggles to render thousands of items it'll never look at. As soon as a list grows beyond a few hundred entries, "send everything" stops being viable.

**What it does:**
**Pagination** splits the list into smaller pages — the client asks for page N at size M, and the server returns just that slice along with the total page count. The database does less work per request, the response is small enough to render fast, and the client stays in control of how much it pulls.

**In code — `GetGamesEndpoint`:**

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

**The problem:**
Endpoints with several query-string parameters (page number, page size, search term, sort, filter) end up with bloated handler signatures listing each one individually. Worse, Minimal API assumes any object parameter is a JSON body, so the obvious refactor — group them into a DTO — silently breaks binding.

**What it does:**
`[AsParameters]` tells ASP.NET Core to bind a complex object from the request's query string, route, and headers instead of from the JSON body. Each property is mapped by name, so a single record can replace half a dozen positional parameters and keep the handler signature clean.

**In code:**
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

**The problem:**
Pagination shrinks page size, but it doesn't help a user looking for one specific item — they'd still page through the entire catalog to find it. And filtering on the client (download all, then `Array.filter`) defeats the point of pagination in the first place.

**What it does:**
**Search filtering** pushes the predicate down to the database via a query parameter, so the server returns only matching rows. Combined with pagination, the API limits both *which* records come back and *how many*, making large catalogs browsable in either direction.

**In code — `GetGamesEndpoint`:**

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

**The problem:**
`.Include()` only loads one level deep. For relationships on relationships — a basket has items, each item has a game — the second hop is silently `null` unless you ask for it explicitly. Accessing it then either throws or quietly drops data from the response.

**What it does:**
`ThenInclude()` chains off a previous `.Include()` call to load a navigation property on the already-included entity. The whole graph (`Basket → Items → Game`) loads in one query, so endpoints that traverse two levels of relationships still take a single round trip.

**In code — `GetBasketEndpoints`:**
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

**The problem:**
JSON cannot carry binary file data, so any endpoint that needs an upload alongside regular fields has to accept `multipart/form-data`. But Minimal API's default assumption is "complex parameter = JSON body" — without telling the framework otherwise, the endpoint sees `null` fields and confused binding errors.

**What it does:**
`[FromForm]` switches the binding source for that parameter from JSON body to form body (`multipart/form-data` or `application/x-www-form-urlencoded`). Scalar properties bind from named form fields and `IFormFile` properties bind from the file parts.

**In code:**
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

**The problem:**
File uploads arrive as binary data in a multipart HTTP body, mixed with text fields, boundary markers, and per-part headers. Parsing that by hand is fiddly and easy to get wrong — and the upload itself needs validation (size, type, safe naming) before the file ever touches disk.

**What it does:**
`IFormFile` is ASP.NET Core's typed wrapper around an uploaded file part. It exposes the file name, content type, length, and a stream — letting endpoints validate the upload (size cap, allowed extensions) and stream it to disk without ever re-parsing the multipart body.

**In code — `FileUploader.UploadFileAsync`:**

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

