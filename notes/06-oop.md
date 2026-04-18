## Object-Oriented Programming

### The Four Pillars

#### Encapsulation

Bundling data and the methods that operate on it into a single unit (a class), while restricting direct access to internal state.

**Access modifiers:**

| Modifier | Visible to |
|---|---|
| `public` | Everyone |
| `private` | This class only |
| `protected` | This class + derived classes |
| `internal` | This assembly only |
| `protected internal` | This assembly OR derived classes |

**Why hide state:** Prevents callers from putting an object into an invalid state. If `_stock` is `private`, no external code can set it to `-1`. The class controls all mutations through its own methods.

```csharp
public class Product
{
    private int _stock;

    public int Stock => _stock; // read-only from outside

    public void AddStock(int quantity)
    {
        if (quantity <= 0) throw new ArgumentException("Must be positive");
        _stock += quantity;
    }
}
```

---

#### Abstraction

Exposing *what* something does while hiding *how* it does it. Callers work against a contract (interface or abstract class), not an implementation.

**Abstract class vs interface:**

| | Abstract class | Interface |
|---|---|---|
| Can have fields | Yes | No (only properties) |
| Can have constructor | Yes | No |
| Can have implemented methods | Yes | Yes (C# 8+ default methods) |
| A class can inherit from | One | Many |
| Use when | Sharing code + partial impl | Defining a contract only |

```csharp
// Abstract class — shared base with some implementation
public abstract class BaseRepository<T>
{
    protected readonly DbContext _db;

    protected BaseRepository(DbContext db) => _db = db;

    public abstract Task<T?> GetByIdAsync(int id); // must override
    public async Task SaveAsync() => await _db.SaveChangesAsync(); // shared impl
}

// Interface — pure contract, no state
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(int id);
    Task<List<Product>> GetAllAsync();
    Task AddAsync(Product product);
}
```

---

#### Inheritance

A derived class acquires the members of its base class and can extend or specialize them.

```csharp
public class Entity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class Product : Entity  // Product IS-A Entity
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}
```

**Constructor chaining with `base()`:**

```csharp
public class AuditedEntity
{
    public int Id { get; set; }
    public string CreatedBy { get; set; }

    public AuditedEntity(string createdBy)
    {
        CreatedBy = createdBy;
    }
}

public class Order : AuditedEntity
{
    public decimal Total { get; set; }

    public Order(string createdBy, decimal total) : base(createdBy)
    {
        Total = total;
    }
}
```

**When NOT to use inheritance:**
- When the relationship is "has-a" not "is-a" — prefer composition
- When you only want to reuse code — extract a helper or service instead
- When the hierarchy would go deeper than two levels — it becomes brittle
- When implementing multiple behaviors — use interfaces + composition

---

#### Polymorphism

The ability of different types to be treated as the same base type, with each type providing its own behaviour.

**`virtual` / `override` (runtime polymorphism):**

```csharp
public class Discount
{
    public virtual decimal Apply(decimal price) => price;
}

public class PercentageDiscount : Discount
{
    private readonly decimal _percent;
    public PercentageDiscount(decimal percent) => _percent = percent;

    public override decimal Apply(decimal price) => price * (1 - _percent / 100);
}

public class FixedDiscount : Discount
{
    private readonly decimal _amount;
    public FixedDiscount(decimal amount) => _amount = amount;

    public override decimal Apply(decimal price) => price - _amount;
}

// Caller doesn't care which type it is:
Discount d = new PercentageDiscount(10);
decimal final = d.Apply(100m); // 90
```

**Method hiding with `new` (compile-time, rarely desired):**

```csharp
public class Base
{
    public string Describe() => "Base";
}

public class Derived : Base
{
    public new string Describe() => "Derived"; // hides, not overrides
}

Base obj = new Derived();
obj.Describe(); // returns "Base" — the base version is called!
```

Use `new` only intentionally. The fact that it breaks through a base reference is usually a bug when seen unexpectedly.

| | `virtual/override` | `new` |
|---|---|---|
| Resolution | At runtime (the actual type) | At compile-time (the declared type) |
| Intent | Extend/replace base behaviour | Intentionally hide base member |
| Common? | Yes | Rarely |

---

### Getters and Setters

C# properties are the standard way to expose (and control) access to data.

**Auto-property:**

```csharp
public string Name { get; set; }
```

**Read-only (set once at construction):**

```csharp
public int Id { get; }          // set only in constructor
public string Slug { get; init; } // set only during object initialization
```

**Computed / expression-bodied:**

```csharp
public string FullLabel => $"{Name} ({Platform})"; // no backing field, computed on demand
```

**Private setter (readable by all, settable only inside class):**

```csharp
public int ViewCount { get; private set; }

public void IncrementViews() => ViewCount++;
```

**Validation in setter:**

```csharp
private decimal _price;
public decimal Price
{
    get => _price;
    set
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "Price cannot be negative");
        _price = value;
    }
}
```

**Realistic model class combining all of the above:**

```csharp
public class Order
{
    // Auto-property, set by EF Core
    public int Id { get; set; }

    // init-only — set at creation, never mutated
    public string CustomerId { get; init; } = string.Empty;

    // Computed from line items
    public decimal Total => LineItems.Sum(li => li.Subtotal);

    // Private setter — only this class changes status
    public OrderStatus Status { get; private set; } = OrderStatus.Pending;

    // Validated price with a full property
    private decimal _shippingCost;
    public decimal ShippingCost
    {
        get => _shippingCost;
        set
        {
            if (value < 0) throw new ArgumentOutOfRangeException(nameof(value));
            _shippingCost = value;
        }
    }

    public List<OrderLineItem> LineItems { get; set; } = [];

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending orders can be confirmed.");
        Status = OrderStatus.Confirmed;
    }
}
```

---

### Interfaces in Depth

An interface is a pure contract — a list of members that any implementing class must provide.

**Defining and implementing:**

```csharp
public interface IOrderService
{
    Task<Order> GetOrderAsync(int id);
    Task<Order> PlaceOrderAsync(PlaceOrderRequest request);
    Task CancelOrderAsync(int id);
}

public class OrderService : IOrderService
{
    public async Task<Order> GetOrderAsync(int id) { /* ... */ }
    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request) { /* ... */ }
    public async Task CancelOrderAsync(int id) { /* ... */ }
}
```

**Why ASP.NET Core uses interfaces everywhere:**
- DI container resolves `IOrderService` at runtime — you register `OrderService` as the implementation
- Tests can inject a fake `IOrderService` without touching the database
- Swapping implementations (e.g. email vs SMS notifications) requires no change to callers

**`IFoo` / `FooService` naming convention:**

| Name | Role |
|---|---|
| `IOrderService` | Contract (interface) |
| `OrderService` | Default implementation |
| `CachedOrderService` | Decorator / alternative impl |

**Multiple interface implementation:**

```csharp
public class ProductService : IProductService, IProductSearchService, IDisposable
{
    // must implement all members of all three interfaces
}
```

**Default interface methods (C# 8+):**

Interfaces can provide a default implementation so existing implementations don't break when you add a new member.

```csharp
public interface INotificationService
{
    Task SendAsync(string message);

    // Default — classes can override or rely on this
    Task SendBatchAsync(IEnumerable<string> messages)
    {
        return Task.WhenAll(messages.Select(SendAsync));
    }
}
```

---

### Dependency Injection

DI is the practice of passing dependencies into a class rather than having the class construct them itself. ASP.NET Core has a built-in DI container.

**Constructor injection (the standard pattern):**

```csharp
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IOrderRepository orders, ILogger<OrderService> logger)
    {
        _orders = orders;
        _logger = logger;
    }
}
```

The container sees the constructor parameters, resolves each one, and provides them automatically.

**The three lifetimes:**

| Lifetime | One instance per… | Use for |
|---|---|---|
| `Singleton` | App lifetime | Stateless services, caches, config wrappers |
| `Scoped` | HTTP request | DB contexts, services that need per-request state |
| `Transient` | Each resolution | Lightweight stateless utilities |

Rules of thumb:
- Never inject a `Scoped` service into a `Singleton` — the scoped service outlives the request
- `DbContext` is always `Scoped` (EF Core default via `AddDbContext`)
- Prefer `Scoped` for application services; use `Singleton` only when you have explicit reason

**Registering in `Program.cs`:**

```csharp
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddSingleton<ICurrencyConverter, CurrencyConverter>();
builder.Services.AddTransient<IEmailSender, SmtpEmailSender>();
```

**Full example — interface → implementation → registration → injection:**

```csharp
// 1. Interface
public interface IProductService
{
    Task<List<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(int id);
}

// 2. Implementation
public class ProductService : IProductService
{
    private readonly IProductRepository _repo;

    public ProductService(IProductRepository repo) => _repo = repo;

    public Task<List<Product>> GetAllAsync() => _repo.GetAllAsync();
    public Task<Product?> GetByIdAsync(int id) => _repo.GetByIdAsync(id);
}

// 3. Registration (Program.cs)
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IProductService, ProductService>();

// 4. Injection into a controller / endpoint group
public class ProductsController : ControllerBase
{
    private readonly IProductService _products;

    public ProductsController(IProductService products) => _products = products;

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        Ok(await _products.GetAllAsync());
}
```

---

### Design Patterns Used in ASP.NET Core

#### Repository Pattern

Abstracts data access behind an interface so the rest of the app never touches EF Core directly.

```csharp
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id);
    Task<List<T>> GetAllAsync();
    Task AddAsync(T entity);
    Task UpdateAsync(T entity);
    Task DeleteAsync(int id);
}

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext _db;
    protected readonly DbSet<T> _set;

    public Repository(AppDbContext db)
    {
        _db = db;
        _set = db.Set<T>();
    }

    public Task<T?> GetByIdAsync(int id) => _set.FindAsync(id).AsTask();
    public Task<List<T>> GetAllAsync() => _set.ToListAsync();
    public async Task AddAsync(T entity) { await _set.AddAsync(entity); await _db.SaveChangesAsync(); }
    public async Task UpdateAsync(T entity) { _set.Update(entity); await _db.SaveChangesAsync(); }
    public async Task DeleteAsync(int id)
    {
        var entity = await GetByIdAsync(id);
        if (entity is not null) { _set.Remove(entity); await _db.SaveChangesAsync(); }
    }
}

// Specific repository extends the generic one
public interface IProductRepository : IRepository<Product>
{
    Task<List<Product>> GetByCategoryAsync(string category);
}
```

---

#### Service Layer Pattern

The service layer sits between controllers and repositories. Controllers handle HTTP; services handle business logic; repositories handle persistence.

```csharp
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly IProductRepository _products;

    public OrderService(IOrderRepository orders, IProductRepository products)
    {
        _orders = orders;
        _products = products;
    }

    public async Task<Order> PlaceOrderAsync(PlaceOrderRequest request)
    {
        // business logic here, not in the controller
        var product = await _products.GetByIdAsync(request.ProductId)
            ?? throw new NotFoundException("Product not found");

        var order = new Order { ProductId = product.Id, Total = product.Price };
        await _orders.AddAsync(order);
        return order;
    }
}
```

---

#### Options Pattern (`IOptions<T>`)

Binds a configuration section to a typed class.

```csharp
// Settings class
public class StripeSettings
{
    public string SecretKey { get; set; } = string.Empty;
    public string WebhookSecret { get; set; } = string.Empty;
}

// Registration (Program.cs)
builder.Services.Configure<StripeSettings>(
    builder.Configuration.GetSection("Stripe"));

// Consumption
public class PaymentService
{
    private readonly StripeSettings _settings;

    public PaymentService(IOptions<StripeSettings> options)
    {
        _settings = options.Value;
    }
}
```

`appsettings.json`:
```json
{
  "Stripe": {
    "SecretKey": "sk_test_...",
    "WebhookSecret": "whsec_..."
  }
}
```

---

#### Decorator Pattern

Wraps an existing implementation to add behaviour without modifying it. Middleware is the most visible example — each middleware wraps the next.

```csharp
public class CachedProductService : IProductService
{
    private readonly IProductService _inner;
    private readonly IMemoryCache _cache;

    public CachedProductService(IProductService inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        return await _cache.GetOrCreateAsync("all-products", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return await _inner.GetAllAsync(); // delegate to the real service
        }) ?? [];
    }
}
```

---

#### Strategy Pattern

Encapsulates a family of algorithms behind a common interface so the caller can swap them at runtime.

```csharp
public interface IDiscountStrategy
{
    decimal Apply(decimal price);
}

public class PercentageOff : IDiscountStrategy
{
    private readonly decimal _percent;
    public PercentageOff(decimal percent) => _percent = percent;
    public decimal Apply(decimal price) => price * (1 - _percent / 100);
}

public class FixedAmountOff : IDiscountStrategy
{
    private readonly decimal _amount;
    public FixedAmountOff(decimal amount) => _amount = amount;
    public decimal Apply(decimal price) => price - _amount;
}

public class PricingService
{
    public decimal GetFinalPrice(decimal price, IDiscountStrategy strategy)
        => strategy.Apply(price);
}
```

---

### Useful C# OOP Features

#### `record` Types

`record` creates an immutable, value-equality class — ideal for DTOs that carry data without behaviour.

```csharp
public record CreateProductRequest(string Name, decimal Price, int GenreId);

public record ProductDto(int Id, string Name, decimal Price, string Genre);

// Records have value equality by default:
var a = new ProductDto(1, "Elden Ring", 59.99m, "RPG");
var b = new ProductDto(1, "Elden Ring", 59.99m, "RPG");
Console.WriteLine(a == b); // true — compares values, not references

// Non-destructive mutation with 'with':
var discounted = a with { Price = 49.99m };
```

---

#### Generics

Write code that works with any type while preserving type safety.

```csharp
// Generic service method
public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public interface IProductRepository
{
    Task<PagedResult<Product>> GetPagedAsync(int page, int pageSize);
}

// Generic extension method on IRepository<T>
public static class RepositoryExtensions
{
    public static async Task<T> GetByIdOrThrowAsync<T>(
        this IRepository<T> repo, int id) where T : class
    {
        return await repo.GetByIdAsync(id)
            ?? throw new NotFoundException($"{typeof(T).Name} {id} not found");
    }
}
```

---

#### `sealed` Classes

Prevents further inheritance. Use when a class is a final implementation that should never be subclassed.

```csharp
public sealed class OrderConfirmationEmail
{
    // No one should extend this — it's a concrete implementation detail
}
```

The compiler can also apply minor optimizations since it knows no virtual dispatch is needed.

---

#### Primary Constructor Syntax (C# 12 / .NET 8+)

Constructor parameters are declared directly on the class, removing the need for separate field declarations and constructor body.

```csharp
// Before (C# 11 and earlier)
public class OrderService : IOrderService
{
    private readonly IOrderRepository _orders;
    private readonly ILogger<OrderService> _logger;

    public OrderService(IOrderRepository orders, ILogger<OrderService> logger)
    {
        _orders = orders;
        _logger = logger;
    }
}

// After (C# 12 primary constructor)
public class OrderService(IOrderRepository orders, ILogger<OrderService> logger) : IOrderService
{
    public async Task<Order?> GetOrderAsync(int id)
    {
        logger.LogInformation("Fetching order {Id}", id);
        return await orders.GetByIdAsync(id);
    }
}
```

Parameters are in scope for the entire class body. Note: they are not automatically stored as fields — if you need to expose them or store them beyond construction, assign them to a field/property yourself.

---

### The Standard Layered Architecture Pattern

The canonical ASP.NET Core architecture separates concerns into three layers. Each layer depends only on the abstraction (interface) of the layer below it — never on a concrete type.

```
HTTP Request
    │
    ▼
Controller          ← handles HTTP, maps to DTOs, delegates to service
    │ uses IOrderService
    ▼
OrderService        ← business logic, orchestrates repositories
    │ uses IOrderRepository
    ▼
OrderRepository     ← data access, talks to EF Core / DB
    │
    ▼
Database
```

**Why each layer depends on abstractions:**
- Controller doesn't care if orders come from SQL, NoSQL, or a mock — it calls `IOrderService`
- `OrderService` doesn't care how data is persisted — it calls `IOrderRepository`
- Tests can inject a fake `IOrderRepository` to test `OrderService` in isolation

**Minimal complete example:**

```csharp
// --- Domain ---
public class Order
{
    public int Id { get; set; }
    public string CustomerId { get; init; } = string.Empty;
    public decimal Total { get; set; }
}

// --- Repository layer ---
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(int id);
    Task AddAsync(Order order);
}

public class OrderRepository(AppDbContext db) : IOrderRepository
{
    public Task<Order?> GetByIdAsync(int id) =>
        db.Orders.FindAsync(id).AsTask();

    public async Task AddAsync(Order order)
    {
        db.Orders.Add(order);
        await db.SaveChangesAsync();
    }
}

// --- Service layer ---
public interface IOrderService
{
    Task<Order?> GetOrderAsync(int id);
    Task<Order> PlaceOrderAsync(string customerId, decimal total);
}

public class OrderService(IOrderRepository orders) : IOrderService
{
    public Task<Order?> GetOrderAsync(int id) => orders.GetByIdAsync(id);

    public async Task<Order> PlaceOrderAsync(string customerId, decimal total)
    {
        var order = new Order { CustomerId = customerId, Total = total };
        await orders.AddAsync(order);
        return order;
    }
}

// --- Controller layer ---
[ApiController]
[Route("orders")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(int id)
    {
        var order = await orderService.GetOrderAsync(id);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost]
    public async Task<IActionResult> Place([FromBody] PlaceOrderRequest request)
    {
        var order = await orderService.PlaceOrderAsync(request.CustomerId, request.Total);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }
}

// --- Registration (Program.cs) ---
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
```

Every dependency flows inward through interfaces. Swapping `OrderRepository` for a different storage backend, or `OrderService` for a decorated version, requires changing only the one registration line in `Program.cs`.

---

