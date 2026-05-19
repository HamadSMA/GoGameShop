## Testing

### Why automated tests

**The problem:**
Software changes break things. A refactor that looks safe can ship a bug. A new feature can quietly regress an old one. Without a safety net, the only way to catch these is to run the application by hand and click through every flow that might be affected, every time anything changes. That scales linearly with feature count and team size, and it scales terribly: by the time the team is large enough to ship interesting work, manual regression testing has become a full-time job that nobody wants to do.

**What it does:**
**Automated tests** are programs that run other programs with known inputs and assert on the outputs. They live next to the production code, run on every commit in CI, and fail loudly when behavior changes. A failing test catches a regression in seconds instead of in production; a passing test gives you the confidence to refactor or upgrade dependencies without manually re-verifying every code path.

What a good test suite buys you:
- **Regression safety**: changes that break existing behavior fail tests before they merge
- **Documentation**: each test's name and body shows how the code under test is supposed to behave
- **Design pressure**: code that's hard to test is usually hard to use; writing tests early surfaces design problems while they're cheap to fix
- **Refactoring courage**: with green tests, you can change internals freely as long as the public contract holds

What a test suite does **not** buy you: proof of correctness. Tests prove the cases you wrote work; they say nothing about the cases you didn't think of. Coverage helps but doesn't substitute for thinking.

This project uses **two kinds** of automated tests: unit tests and integration tests. The rest of this file explains what each one is, then walks through every test in the suite.

---
### Unit tests

**The problem:**
The smallest unit of behavior in an object-oriented system is usually a single method on a class. When that method has a bug, you want a test that fails the moment the bug appears, not a sprawling end-to-end test that's been broken for three other reasons. You also want the test to run in milliseconds so you can keep a "watch" loop running while you code, instead of waiting for a database or a network round trip every time you save a file.

**What it does:**
A **unit test** exercises one method (or one tightly coupled cluster of methods) **in complete isolation** from the outside world. Three properties define it:

1. **Isolated.** No database, no HTTP, no file system, no clock, no network. If the method has dependencies (a repository, a logger, an HTTP client), the test substitutes them with fakes that return whatever the test wants. Building those fakes is what mocking libraries are for.
2. **Fast.** Milliseconds per test. A unit-test suite of a thousand tests should run in single-digit seconds.
3. **Deterministic.** Same input, same result, every time. A test that fails intermittently ("flaky") is worse than no test: it teaches developers to ignore failing tests.

A unit test answers one question: "given these inputs and these dependency behaviors, does the method produce the right output?"

When unit tests are the right tool:
- Pure functions (calculators, validators, formatters, mappers)
- Domain logic with branching (authorization handlers, pricing rules)
- Anything where the **interesting behavior lives in code you wrote**, not in the wiring between components

When they are **not** the right tool:
- "Does my route map to my handler?" : that's integration
- "Does my SQL actually return what I expect?" : that's integration
- "Does my DI container hand me the right implementation?" : that's integration

---
### The AAA pattern and test naming

**The problem:**
A test that runs and a test that's readable are not the same thing. When a test fails six months from now, the person reading it has none of the context the author had. If the test body is a tangle, the fix is to read the production code first to figure out what the test was trying to prove, which defeats half the value of writing the test.

**What it does:**
Two conventions: **Arrange-Act-Assert** for the body, and `Method_Scenario_ExpectedResult` for the name.

**Arrange-Act-Assert** divides every test into three sections:
- **Arrange**: set up the inputs and any fakes
- **Act**: call the thing under test, exactly once
- **Assert**: check the result

```csharp
[Fact]
public void Add_TwoPositiveNumbers_ReturnsSum()
{
    // Arrange
    var calculator = new Calculator();

    // Act
    int result = calculator.Add(2, 3);

    // Assert
    Assert.Equal(5, result);
}
```

The single-Act rule matters: if a test calls the method under test twice, it's testing two scenarios, and one of them probably belongs in a separate test.

**`Method_Scenario_ExpectedResult`** makes failures self-explanatory. The test runner shows just the method name when a test fails; the name alone should tell you what broke. `Add_NegativeAndPositive_ReturnsDifference` is useful; `TestAdd1` is not. The cost is verbose method names, which is a price worth paying.

---
### xUnit basics

**The problem:**
.NET has three major test frameworks (xUnit, NUnit, MSTest) and you need to pick one and learn its idioms before you can write any tests. They cover the same ground with slightly different vocabularies.

**What it does:**
**xUnit** is the most-used framework for new .NET projects (and the one this project uses). Its API is small.

The pieces worth knowing on day one:

- **`[Fact]`**: marks a parameterless test method.
- **`[Theory]` + `[InlineData(...)]`**: a parameterized test. xUnit runs the method once per `InlineData` row, treating each row as a separate test case in the output.

```csharp
[Theory]
[InlineData(2, 3, 5)]
[InlineData(-1, 1, 0)]
[InlineData(0, 0, 0)]
public void Add_VariousInputs_ReturnsSum(int a, int b, int expected)
{
    Assert.Equal(expected, new Calculator().Add(a, b));
}
```

- **`Assert.*`**: assertion methods. `Equal`, `NotEqual`, `True`, `False`, `Null`, `NotNull`, `Contains`, `DoesNotContain`, `Empty`, `NotEmpty`, `Throws<T>`, `ThrowsAsync<T>`, `Record.Exception(...)`.
- **`IClassFixture<T>`**: tells xUnit "build one `T` and pass it to my constructor; share it across every test in this class." Used for expensive setup like booting a web host.
- **`IAsyncLifetime`**: an interface a test class can implement for async setup and teardown across the whole class.

xUnit creates a **new instance of the test class for every test** by default. State shared across tests has to go through a fixture; instance fields are reset every time. That isolation is a feature: one test cannot accidentally depend on another.

---
### Mocking with NSubstitute

**The problem:**
A class under test usually has dependencies declared as interfaces: `ILogger<T>`, an `IBasketRepository`, an `IHttpClientFactory`. The unit test needs to hand it stand-ins for those dependencies, because the real implementations would pull in a database, an HTTP server, or a logging sink. Hand-writing a fake class for every interface every test needs would be miserable.

**What it does:**
A **mocking library** generates fake implementations of interfaces on the fly. The test asks for one, configures any return values it cares about, and hands it to the class under test.

This project uses **NSubstitute**. The API has three moves to know:

**Create a substitute (a fake):**
```csharp
var logger = Substitute.For<ILogger<MyService>>();
```

**Configure a return value:**
```csharp
var repo = Substitute.For<IBasketRepository>();
repo.GetByUserIdAsync(Arg.Any<Guid>()).Returns(new CustomerBasket());
```

`Arg.Any<T>()` matches any value of that type; `Arg.Is<T>(predicate)` matches values that satisfy a condition.

**Verify a call happened:**
```csharp
await service.SaveBasketAsync(basket);
await repo.Received().SaveAsync(basket);             // expect at least one call
await repo.Received(1).SaveAsync(basket);            // expect exactly one
await repo.DidNotReceive().DeleteAsync(Arg.Any<Guid>()); // expect zero
```

The two competing libraries in .NET are **Moq** (older, more imperative API: `mock.Setup(x => x.M(...)).Returns(...)`) and **NSubstitute** (newer, terser, fewer ceremonial wrappers). They do the same job; pick one and stick with it.

---
### Integration tests

**The problem:**
Unit tests can all pass while the application is still broken. A handler can be correct in isolation while not being registered in DI. A route can be wired to a handler that throws when EF Core actually runs the query. An auth policy can be configured wrong in a way no individual class would ever notice. The only test that catches these is one that exercises the wiring: middleware, DI, routing, model binding, the real database, the real serializer.

**What it does:**
An **integration test** boots multiple components together and asserts on the result. For an ASP.NET Core API, that means:

- The full HTTP pipeline (routing, model binding, middleware, auth, exception handling)
- The real DI container, with every service registered the way `Program.cs` registers them
- A real (or near-real) database, so EF Core actually generates and executes SQL
- The real JSON serializer with all configured options

An integration test does **not** mean "uses a real network." For ASP.NET Core, the framework's `WebApplicationFactory<T>` boots the app **in-process**: no TCP socket opens, but the request still flows through every piece of middleware as if it were a real HTTP call.

Trade-offs versus unit tests:
- **Slower**: hundreds of milliseconds per test instead of single-digit milliseconds. A suite of fifty integration tests takes ten or twenty seconds; a suite of two thousand is a coffee break.
- **Broader**: one integration test that hits an endpoint exercises dozens of files. A failure can have many causes, and the test name alone often does not pinpoint the bug.
- **Fewer of them**: write enough to cover the wiring and the auth paths. Cover the business-logic permutations in unit tests, where they run in milliseconds.

---
### `WebApplicationFactory<T>` and in-process testing

**The problem:**
A naive "integration test" approach would be: `dotnet run` the API in a background process, give it a port, point an `HttpClient` at `http://localhost:5002`, then `dotnet test`. That works but is slow, brittle (port conflicts, cleanup of leftover processes), and a pain in CI.

**What it does:**
**`WebApplicationFactory<TEntryPoint>`** (from the `Microsoft.AspNetCore.Mvc.Testing` NuGet package) boots the ASP.NET Core application **in the same process as the test**. The `TEntryPoint` type parameter points at the `Program` class so the framework knows where to start. The factory:

1. Runs the same startup code `Program.cs` would run, with the chance to override registrations
2. Replaces the real Kestrel server with an in-memory transport that has no TCP socket
3. Hands you an `HttpClient` whose `HttpMessageHandler` routes calls directly into the in-process pipeline

```csharp
public class MyEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public MyEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetThing_ReturnsOk()
    {
        var response = await _client.GetAsync("/things/1");
        response.EnsureSuccessStatusCode();
    }
}
```

For the test project to find `Program`, the minimal-API `Program.cs` needs `public partial class Program { }` at the bottom, because the implicit `Program` generated from top-level statements is `internal` by default.

---
### Swapping services in tests

**The problem:**
The whole point of integration tests is to run real code, but a few pieces have to be substituted: a real database means tests stomp on each other and on dev data; a real identity provider means tests need a running Keycloak.

**What it does:**
Subclass `WebApplicationFactory<T>` and override `ConfigureWebHost`. The hook runs after `Program.cs` has registered all its services but before the app is built, which is the right seam for surgery: find the registration to replace, remove it, add a new one.

```csharp
public class MyFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Find and remove the existing registration
            var descriptor = services.SingleOrDefault(d =>
                d.ServiceType == typeof(DbContextOptions<MyDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            // Re-register with the test implementation
            services.AddDbContext<MyDbContext>(opt => opt.UseSqlite("..."));
        });
    }
}
```

The same pattern works for any service: replace `IEmailSender` with a no-op, replace an `IClock` with a fixed time, replace an external HTTP client with one that returns canned responses. Resist the temptation to swap too much: every fake brings the test further from production and risks hiding real bugs.

---
### SQLite in-memory for tests

**The problem:**
Integration tests need a database. The production database (SQL Server, PostgreSQL) requires a separate server, which complicates CI and slows tests. A real local file is fine but has to be cleaned up between runs, and parallel tests can step on each other. Pure mocks for the DbContext drift from real EF Core behavior and miss SQL bugs.

**What it does:**
**SQLite in `:memory:` mode** is a fully functional SQL engine that lives entirely in process memory. EF Core's SQLite provider talks to it the same way it talks to a file, so the test exercises the real query translator, change tracker, and migrations.

The catch: each `:memory:` database is **per-connection**. Open a connection, create a table, close the connection, the database is gone. EF Core opens and closes connections on demand, so a naive setup loses data immediately. The fix: open one `SqliteConnection` in the test fixture and keep it open for the fixture's entire lifetime, then hand the same connection to every DbContext:

```csharp
private readonly SqliteConnection _connection;

public MyFactory()
{
    _connection = new SqliteConnection("Data Source=:memory:");
    _connection.Open();
}

protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureServices(services =>
    {
        // remove production DbContext registration ...
        services.AddDbContext<MyDbContext>(opt => opt.UseSqlite(_connection));
    });
}

protected override void Dispose(bool disposing)
{
    if (disposing) _connection.Dispose();
    base.Dispose(disposing);
}
```

The alternative is **Testcontainers**: spin up a real SQL Server, PostgreSQL, or whatever the production database is, inside a throwaway Docker container per test class. Higher fidelity (catches dialect-specific bugs), slower, requires Docker on every machine that runs tests. Worth it on bigger projects; overkill for a learning project.

---
### `IClassFixture<T>` and shared setup

**The problem:**
Booting an ASP.NET Core app, running migrations, and seeding a database takes a second or two. If xUnit did that for every `[Fact]`, a fifty-test integration suite would take a minute even before any of the actual HTTP calls. At the same time, you don't want tests to share state and step on each other.

**What it does:**
**`IClassFixture<T>`** is xUnit's hook for "create one instance of `T` and reuse it across every test in this class." The fixture is built before the first test runs and disposed after the last one finishes. Test class:

```csharp
public class MyTests : IClassFixture<MyFactory>
{
    private readonly HttpClient _client;

    public MyTests(MyFactory factory) // xUnit injects the shared fixture
    {
        _client = factory.CreateClient();
    }
}
```

Important property: **xUnit creates a separate fixture per test class**. Two different test classes that both use `IClassFixture<MyFactory>` get two different `MyFactory` instances and therefore two different in-memory databases. Tests in different classes do not share state.

For setup that must be shared across **multiple** test classes (rare), `ICollectionFixture<T>` plus `[Collection("name")]` does the same trick at collection scope.

---
### Test authentication handlers

**The problem:**
Endpoints behind `[Authorize]` need an authenticated request. In production, that means a JWT issued by Keycloak. In tests, running Keycloak is overkill, and minting real JWTs against a test key just to test "the auth-required endpoints work" adds complexity that obscures what the test is really proving.

**What it does:**
Register a tiny **test authentication scheme** that builds a `ClaimsPrincipal` from a request header, then point the framework's default scheme at it during the test run. Production code stays unchanged; tests get a one-line way to say "this request is from user X with role Y."

```csharp
public class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Test";
    public const string UserIdHeader = "X-Test-User-Id";

    public TestAuthHandler(/* ... base ctor args ... */) : base(/* ... */) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UserIdHeader, out var userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new[] { new Claim("sub", userId.ToString()) /* + scope, roles */ };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
```

In the factory, register the scheme and override the default:

```csharp
services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

services.PostConfigure<AuthenticationOptions>(options =>
{
    options.DefaultScheme = TestAuthHandler.SchemeName;
    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
});
```

`PostConfigure` runs last, so it wins over whatever `Program.cs` set. Tests then attach the header per request:

```csharp
client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, userId.ToString());
```

No Keycloak, no JWT signing, no metadata fetch.

---
### Tests in this project

**The problem:**
Reading generic guidance about unit and integration tests is not the same as knowing what this project's test suite actually covers. Maintainers (including future-you) need a concrete map: what is tested, what each test exists to catch, and why each was written.

**What it does:**
The test layout under `Backend/tests/` has two projects:

```
Backend/tests/
├── GoGameShop.Api.UnitTests/        (xUnit + NSubstitute)
└── GoGameShop.Api.IntegrationTests/ (xUnit + Mvc.Testing + EF Core SQLite)
```

The unit-test project takes a project reference to the API project and a NuGet reference to `NSubstitute`. The integration-test project adds `Microsoft.AspNetCore.Mvc.Testing` (for `WebApplicationFactory<T>`) and `Microsoft.EntityFrameworkCore.Sqlite` (for the in-memory database swap).

Both projects mirror the source folder structure: a test for a class in `src/GoGameShop.Api/Features/Baskets/Authorization/BasketAuthorizationHandler.cs` lives at `tests/GoGameShop.Api.UnitTests/Features/Baskets/Authorization/BasketAuthorizationHandlerTests.cs`.

The integration-test project also contains:
- `GoGameShopWebApplicationFactory.cs` : subclass of `WebApplicationFactory<Program>` that swaps in a SQLite in-memory `DbContext` and installs the test authentication scheme as the default.
- `TestAuthHandler.cs` : the test authentication handler. Reads `X-Test-User-Id` and optional `X-Test-User-Roles` headers, attaches the `gogameshop_api.all` scope claim automatically so requests pass the fallback authorization policy, and returns `NoResult` (which the framework turns into a 401) when no user-id header is present.

---
#### The 10 unit tests

The unit-test suite covers the two classes in the project that contain non-trivial **pure logic**: the resource-based authorization handler and the Keycloak claims transformer. Pure logic is where unit tests pay best.

**`BasketAuthorizationHandlerTests`** : five tests against the handler that decides whether the current user is allowed to act on a `CustomerBasket`. The rule is "the user's `sub` claim equals the basket's `Id`, **or** the user has the `Admin` role." Every branch through the handler is covered.

1. **`HandleRequirement_UserIsBasketOwner_Succeeds`**
   Builds a `ClaimsPrincipal` whose `sub` claim equals the basket's `Id`, runs the handler, asserts `context.HasSucceeded == true`. This is the happy-path test for the owner branch.

2. **`HandleRequirement_UserIsAdmin_Succeeds`**
   Builds a principal whose `sub` is **a different GUID** from the basket's `Id`, but with a role claim placing the user in the `Admin` role. Asserts the handler still succeeds. This proves the OR semantics: an admin can act on a basket they do not own.

3. **`HandleRequirement_UserIsNeitherOwnerNorAdmin_DoesNotSucceed`**
   Different sub, no Admin role. Asserts `HasSucceeded == false`. The negative case that locks down "stranger has no access."

4. **`HandleRequirement_SubClaimMissing_DoesNotSucceed`**
   Principal has no `sub` claim at all. The handler's first guard returns early without calling `context.Succeed`. Asserts `HasSucceeded == false`. Covers the early-return branch that the previous three tests cannot reach.

5. **`HandleRequirement_SubClaimNotAGuid_ThrowsFormatException`**
   Principal's `sub` claim is the literal string `"not-a-guid"`. The handler currently calls `Guid.Parse` (not `TryParse`) and therefore throws `FormatException`. The test uses `Assert.ThrowsAsync<FormatException>` to **document the current behavior**. If someone later refactors to `TryParse` and silently treats malformed claims as deny, this test fails and forces a deliberate conversation about which behavior is correct. Tests that lock down defensive-or-buggy behavior are a real category of test, not just happy-path coverage.

**`KeycloakClaimsTransformerTests`** : five tests against the class that takes a Keycloak-style space-delimited `scope` claim (`"openid profile email"`) and explodes it into one claim per scope. Why this matters: ASP.NET Core's authorization layer requires `RequireClaim("scope", "...")` to match each scope individually, not as a substring of a combined claim.

1. **`Transform_MultipleScopesInOneClaim_SplitsIntoSeparateClaims`**
   Input: a single claim with value `"openid profile email"`. Asserts the identity ends up with three separate `scope` claims, one per token. The core happy path.

2. **`Transform_MultipleScopes_RemovesOriginalCombinedClaim`**
   Same input as above. Asserts the original combined claim is no longer present on the identity. Without this, a `RequireClaim("scope", "openid")` check would still see the combined claim and confuse policy evaluation.

3. **`Transform_NoScopeClaim_LeavesIdentityUnchanged`**
   Identity has some other claim (`name`) but no `scope` claim. Asserts the identity's claims list is identical before and after the transform. This locks down the no-op early return for tokens that never had a scope claim.

4. **`Transform_SingleScope_ProducesOneClaim`**
   Input: a single claim with value `"openid"` (no spaces). Asserts the identity ends up with exactly one `scope` claim. Proves the split logic handles the edge case where there's nothing to split.

5. **`Transform_PrincipalIsNull_DoesNotThrow`**
   Builds a `TokenValidatedContext` with `Principal = null`. Uses `Record.Exception` to capture whatever the call throws (or doesn't) and asserts it is `null`. This locks down the null-safety provided by the `?.` operators in the source, so future refactors cannot quietly remove them.

---
#### The 5 integration tests

The integration-test suite covers one happy path, one not-found path, and three authentication paths. Together they verify that the auth wiring actually applies, the seeded data flows through EF Core and JSON, and the basket ownership policy works end-to-end.

**`GetGamesEndpointTests`**

1. **`GetGames_Anonymous_Returns200AndNonEmptyList`**
   `GET /games` with no auth header. Asserts status is `200 OK` and the deserialized `GamesPageDto` has a non-empty `Games` collection.
   **What it proves:** the public catalog endpoint is reachable without authentication (its `.AllowAnonymous()` is actually applied), the EF Core query against SQLite returns rows, the JSON serializer maps everything through, and the seed data ran. If a future change accidentally removes `.AllowAnonymous()` from the games route, this test fails with a `401`.

**`GetGameEndpointTests`**

2. **`GetGame_NonExistentId_Returns404`**
   `GET /games/{random-guid}`. Asserts status is `404 Not Found`.
   **What it proves:** the lookup endpoint actually returns 404 instead of, say, a `500` or a misleading empty `200`. Catches any future change that swaps `dbContext.Games.FindAsync` for something that throws on miss, or that drops the `is null` check.

**`GetBasketEndpointTests`**

3. **`GetBasket_NoAuth_Returns401`**
   `GET /baskets/{random-guid}` with no headers. Asserts status is `401 Unauthorized`.
   **What it proves:** the fallback authorization policy (which requires the `scope` claim with value `gogameshop_api.all`) actually applies to basket endpoints. Catches the very specific bug of someone marking a basket endpoint `.AllowAnonymous()` by mistake, or removing the fallback policy from `AuthorizationExtensions`.

**`UpsertBasketEndpointTests`**

4. **`UpsertBasket_AuthenticatedOwner_Returns204`**
   Generates a random `userId`, authenticates as that user via the `X-Test-User-Id` header, sends `PUT /baskets/{userId}` with an empty `UpsertBasketDto`. Asserts status is `204 No Content`.
   **What it proves:** the full success path of the upsert endpoint: authentication passes, the fallback policy passes (the test handler attaches the scope claim automatically), `BasketAuthorizationHandler` succeeds because the principal's `sub` matches the URL's `userId`, EF Core's `SaveChangesAsync` completes against the in-memory database, and the endpoint returns `204`. If any of those pieces is misconfigured, this test fails. This is the broadest single test in the suite.

5. **`UpsertBasket_AuthenticatedAsDifferentUser_Returns403`**
   Generates two random GUIDs: one for the basket's owner in the URL, one for the **attacker** authenticated via the header. Sends `PUT /baskets/{ownerId}` while authenticated as the attacker. Asserts status is `403 Forbidden`.
   **What it proves:** the ownership check actually runs in the real HTTP pipeline. The fallback policy passes (the attacker still has the scope claim), so the request makes it into the endpoint handler, which loads the basket and calls `IAuthorizationService.AuthorizeAsync` with `BasketAuthorizationHandler`. Because the attacker's `sub` does not match the basket's `Id` and they have no Admin role, the handler does not succeed, the endpoint returns `Results.Forbid()`, and the response is `403`. This is the **most security-relevant** test in the suite: it catches a regression where an unauthorized user could write to someone else's basket.

---
#### Why this set

Three deliberate choices shaped the suite:

- **Unit tests stay where unit tests pay best.** The handler and the claims transformer are pure-logic classes with multiple branches each. Every branch is covered by a unit test that runs in single-digit milliseconds. Their integration with the rest of the app is tested separately and only once each.
- **Integration tests cover the auth wiring, not the auth logic.** The unit tests already prove that `BasketAuthorizationHandler` behaves correctly given various principals. The integration tests prove that the handler is **actually wired into the HTTP pipeline** for the basket endpoints. Nothing in the integration suite re-tests the handler's internal branches; that would be wasted runtime.
- **No test is redundant with another.** Every test fails for a different reason. The two `UpsertBasket` tests look symmetric (success vs forbid) but they prove different things : one proves the success path runs end-to-end including `SaveChangesAsync`; the other proves the ownership check actually blocks unauthorized writes. Either test passing tells you nothing about the other.