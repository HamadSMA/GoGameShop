## Architecture Patterns

> Project organization and API style are decisions you make on **day one** of a codebase, before the first endpoint exists. Changing them later means moving every file. The patterns below are the standard menu — what they solve, when they fit, and how they read in practice.

This file groups patterns into two concerns:

1. **Project organization** — *how the codebase is laid out on disk* (Vertical Slice, Layered, Clean, Onion, Hexagonal, Modular Monolith)
2. **API style** — *how individual endpoints are written* (Minimal APIs, Controller-based MVC, FastEndpoints)

The two are independent: you can pair Vertical Slice with Minimal APIs (this project) or with Controllers; you can pair Clean Architecture with FastEndpoints. Pick from each menu separately.

---

### *Project Organization*

The next six sections are alternative ways to organize the codebase on disk — *which folders exist and what each one owns*. This project uses **Vertical Slice**.

---
### Vertical Slice Architecture

**Vertical slicing** organizes code by **feature** instead of by technical layer. Each folder owns everything one feature needs — endpoint, DTOs, validation, handlers — so adding a feature is adding a folder, and reading a feature is reading one folder. Layered architectures (`Controllers/`, `Services/`, `Repositories/`) scatter the code for one feature across many folders, which means adding "Create Game" requires editing four files in four different layers.

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
├── Baskets/
│   ├── Authorization/             # Per-slice auth handler
│   ├── GetBasket/
│   └── UpsertBasket/
├── Genres/
└── Ratings/
```

Each folder is a self-contained "slice." Adding a new feature means adding a new folder, not modifying multiple existing layers. The shared code that *is* truly shared (DbContext, FileUploader, authorization policies) lives in `Shared/` and `Data/` — but those are intentionally small and rarely touched.

Vertical slicing fits well for small-to-medium APIs with CRUD-shaped workloads and teams that ship feature-by-feature. The Minimal API style pairs naturally with it because each endpoint is one lambda, easy to drop into a slice. Where it starts to strain is heavy domain logic that genuinely needs to be reused across features — slicing tends to push you toward duplicating that logic per slice, which can rot. At that point, mix in a domain layer.

---
### Layered (N-Tier) Architecture

Code is grouped by **technical role**: `Controllers/`, `Services/`, `Repositories/`, `Models/`. Each layer depends only on the one below it. The "shape" of the codebase mirrors a stack of tiers — presentation on top, data at the bottom. The appeal is familiarity: this is what nearly every .NET developer recognizes, and it matches the framework's own defaults and most Microsoft tutorials.

The trade-off is that one feature spans every layer. Adding "Create Game" means changes in `Controllers/`, `Services/`, `Repositories/`, and `Models/` — every edit is a shotgun edit. Repositories on top of EF Core are also usually redundant, since `DbContext` is already a Repository and Unit of Work pattern.

This is the architecture you'll see in most legacy ASP.NET Core codebases. Vertical Slice is the modern reaction to its scaling problems.

---
### Clean Architecture

Standard layered architectures point dependencies *downward*: the presentation layer references services, which reference repositories, which reference EF Core. That means business logic transitively depends on the database driver, the web framework, and external SDKs. Swapping any of them, or unit-testing the business logic in isolation, requires either heavy mocking or framework-aware test scaffolding.

**Clean Architecture** (Robert C. Martin) inverts the dependency direction: the **domain** sits at the center with no outward references; **application** wraps the domain (use cases / handlers); **infrastructure** (EF Core, HTTP clients, file system) and **presentation** (API controllers) sit on the outermost ring and depend *inward*. The dependency rule is: arrows always point toward the center.

**Typical project layout:**
```
src/
├── MyApp.Domain/              # Entities, value objects, domain events
├── MyApp.Application/          # Use cases, interfaces (e.g., IGameRepository)
├── MyApp.Infrastructure/       # EF Core, file storage — implements Application's interfaces
└── MyApp.Api/                  # ASP.NET Core, depends on Application + Infrastructure
```

Clean Architecture fits long-lived, business-rule-heavy systems where the domain logic is the asset: banking, insurance, ERP, multi-channel platforms. For a CRUD API of 20 endpoints, the four-project ceremony costs more than the dependency-direction win earns you.

---
### Onion Architecture

Onion Architecture targets the same problem as Clean Architecture, with slightly different terminology that pre-dated it. Concentric rings, dependencies pointing inward. The innermost ring is **Domain Model**, next is **Domain Services**, then **Application Services**, then the outer ring of **Infrastructure / UI / Tests**. The outer rings know about the inner rings, never the reverse.

In practice, most teams that say "we're using Onion" mean the same thing as Clean Architecture — the pattern is the same idea with an older name. Modern .NET codebases have largely converged on Clean Architecture's framing.

---
### Hexagonal Architecture (Ports and Adapters)

Alistair Cockburn's pattern takes the "inward dependencies" idea further by making it explicit at the boundary level. The application core defines **ports** (interfaces) for everything outside it: a port to receive a request, a port to fetch data, a port to send notifications. **Adapters** plug into those ports — an HTTP adapter, an EF Core adapter, an SMTP adapter. The core has zero knowledge of which adapter is wired up at runtime.

The visual metaphor is a hexagon: the core is the shape, each side is a port, adapters dock from the outside. Multiple adapters can implement the same port — you can run the core via HTTP in production and via a test harness in unit tests, with no core changes.

This pattern has the strongest testability and replaceability story of any architecture, and forces explicit boundaries so no service quietly calls an external system. The cost is ceremony: ports and adapters roughly double the file count, and it only pays off if you actually swap adapters or run the core headless.

Hexagonal, Onion, and Clean Architecture are close cousins. Pick based on the team's vocabulary, not theological differences.

---
### Modular Monolith

Microservices solve scaling and deployment-independence problems but introduce network boundaries, distributed-tracing pain, and eventual-consistency bugs. A small team building one product rarely has the *operational* problems microservices solve, but ends up with all the costs.

A **modular monolith** is a single deployable that internally enforces strict module boundaries — each module owns its own feature folder, its own DbContext schema, its own internal services, and exposes a narrow public surface to other modules (an in-process "API" — usually an interface or a mediator contract). No module reaches into another module's internals; if it needs data, it asks via the contract.

```
src/MyApp.Api/
├── Modules/
│   ├── Catalog/        # Domain, EF, endpoints — internal to this module
│   ├── Orders/         # Same — separate schema, separate logic
│   └── Identity/
└── Shared/             # Truly cross-cutting infrastructure
```

This pattern fits mid-size systems that *might* split into microservices later but don't need to today. The module boundaries already exist, so extraction into separate services later is a refactor, not a rewrite. For tiny apps the module boundaries become noise, and for genuinely independent teams that need separate release cycles it's better to ship microservices from the start.

---

### *API Style*

The next three sections are alternatives for *how individual endpoints are written* inside whichever organization above you picked. This project uses **Minimal APIs**.

---
### Minimal APIs

**Minimal APIs** (since .NET 6, mature in .NET 8/9/10) let you register endpoints as plain lambdas attached to `WebApplication`. Controller-based MVC carries decades of conventions — filters, model binders, attribute routing, action results — that pay off in large applications but feel like ceremony when you're writing 20 CRUD endpoints.

```csharp
app.MapGet("/games/{id}", async (int id, GoGameShopContext db) =>
    await db.Games.FindAsync(id) is Game g
        ? Results.Ok(g.ToDto())
        : Results.NotFound());
```

Routing, parameter binding, DI, OpenAPI metadata, validation, auth attributes — all the framework features still apply, just expressed as method calls on the endpoint instead of attributes on a class.

Minimal APIs fit APIs of any size where each endpoint's logic is small (CRUD, lookups, simple commands). They pair especially well with Vertical Slice — one endpoint per file, one lambda per endpoint. Heavy per-controller cross-cutting (multiple action filters, complex model binding conventions) is still cleaner with controllers, and you can mix both: most endpoints as Minimal APIs, a few legacy controllers alongside.

---
### Controller-Based MVC

Some endpoints share a lot of behavior — same auth setup, same logging, same model-binding rules, same exception handling. Repeating that on every Minimal API lambda is noisy. Controllers organize that shared behavior via a class: endpoints are public methods on a class that inherits `ControllerBase`, and attributes (`[Authorize]`, `[ApiController]`, `[Route("api/[controller]")]`) apply to every action, while filters layer cross-cutting behavior across them.

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = Policies.AdminAccess)]
public class GamesController : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, GoGameShopContext db) => ...
}
```

Controllers fit large APIs with deep per-route cross-cutting, teams who already think in MVC, and codebases that need MVC-specific features like model binding conventions or view rendering (Razor). For small CRUD APIs, the class-and-attribute scaffolding costs more time than it saves. The .NET team itself now leans Minimal APIs in new project templates.

---
### FastEndpoints (REPR Pattern)

Minimal APIs lean on lambdas, which makes very large or behavior-heavy endpoints harder to organize. Controllers organize by *class* (one class, many endpoints), which is sometimes the wrong axis. The **REPR pattern** (Request, Endpoint, Response) argues each endpoint deserves its *own* class — one file, one endpoint, one input shape, one output shape.

**FastEndpoints** is a third-party library (not Microsoft) that implements REPR on top of ASP.NET Core's routing. Each endpoint is a class inheriting `Endpoint<TRequest, TResponse>`, with a `Configure()` method declaring the route and a `HandleAsync()` method doing the work.

```csharp
public class GetGameEndpoint : Endpoint<GetGameRequest, GameDto>
{
    public override void Configure()
    {
        Get("/games/{id:int}");
        AllowAnonymous();
    }
    public override async Task HandleAsync(GetGameRequest req, CancellationToken ct) { ... }
}
```

FastEndpoints pairs cleanly with Vertical Slice — REPR's "one class per endpoint" is the same axis as "one folder per slice." It brings strong opinions about validation (FluentValidation built in), result mapping, and routing. The costs are a third-party dependency and an additional idiom for new teammates to learn.

Worth knowing about, but not the default — Minimal APIs cover most needs, and adopting FastEndpoints is a deliberate architectural commitment.

---
