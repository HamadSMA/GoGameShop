## Async Programming

### Async/Await

**The problem:**
Web servers handle many requests at once. If every request blocks a thread while waiting for a database query, you run out of threads under load and the server stalls.

**What it does:**
`async` and `await` are C# keywords that let you write non-blocking code that reads like synchronous code. An `async` method can pause at an `await` expression and release the thread while it waits for a result — so the thread is free to handle other requests while the database is busy.

**In code:**
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

**The problem:**
An `async` method can't return its real result on the spot — the work hasn't happened yet. The caller still needs *something* back so it can wait for the result later.

**What it does:**
`Task` is C#'s representation of an ongoing asynchronous operation — a promise that "this will eventually produce a result (or finish) in the future." When a method is marked `async`, it returns a `Task` immediately, and the caller can `await` that `Task` to get the result when it's ready.

**Three forms:**

| Type | Meaning |
|------|---------|
| `Task` | Async operation with no return value |
| `Task<T>` | Async operation that returns a value of type `T` |
| `ValueTask<T>` | Lightweight version of `Task<T>` (see section 28) |

**In code:**
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

**The problem:**
Before `async/await` existed, you still needed a way to run code *after* a task completed. Even today, some scenarios need finer control over the continuation than `await` exposes — running only on success, only on failure, or on a specific thread.

**What it does:**
`ContinueWith()` is a method on `Task` that schedules a callback to run when the task completes. It's the older, explicit way to chain async operations and it predates `async/await` syntax — useful to know because you'll encounter it in older codebases and in framework source.

**In code:**
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

