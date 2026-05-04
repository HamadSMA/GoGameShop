# GoGameShop (In Progress)

A production-ready game store API for selling digital game keys. Built with the modern .NET stack, focusing on clean
architecture and minimal API patterns.

---

> **AI Disclosure** — Source code is 100% handwritten. Commit messages are AI-generated. Docs and notes are
> collaborative — AI-assisted, personally reviewed, and edited.

📓 **Dev Notes** — Running notes I take while building this project. [View notes →](./NOTES.md)

---

## Overview

GoGameShop is a backend service designed to manage a catalog of digital games, including features for browsing genres,
ratings, and managing game details (CRUD operations). It leverages the latest .NET features to provide a performant and
scalable foundation.

## Technology Stack

- **Language:** C# 14
- **Framework:** ASP.NET Core 10.0 (Minimal APIs)
- **Database:** SQLite (PostgreSQL migration planned)
- **ORM:** Entity Framework Core 10.0
- **Frontend:** Responsive Frontend (Planned)
- **Payments:** Stripe Integration (Planned)
- **Package Manager:** NuGet (via `dotnet` CLI)

## Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [dotnet-ef](https://learn.microsoft.com/en-us/ef/core/cli/dotnet) Global Tool (for managing migrations)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for running Keycloak locally)

## Setup & Run

1. **Clone the repository:**
   ```bash
   git clone <repository-url>
   cd GoGameShop
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Apply Database Migrations:**
   The application uses SQLite. To set up the initial database:
   ```bash
   dotnet ef database update --project Backend/src/GoGameShop.Api
   ```
   *Note: The application calls `InitializeDbAsync()` on startup, which automatically applies migrations and seeds
   initial data (9 genres, 3 ratings).*

4. **Start Keycloak (local IAM server):**
   ```bash
   cd Backend/localinfra
   docker compose up -d
   ```
   The Keycloak admin console is available at `http://localhost:8080` (bootstrap credentials: `admin` / `admin`). The
   committed `gogameshop-realm.json` can be imported via *Realm settings → Action → Partial import* to set up the realm,
   roles, and clients.

5. **Run the application:**
   ```bash
   dotnet run --project Backend/src/GoGameShop.Api
   ```
   The API will be available at `http://localhost:5078` (or the port specified in `appsettings.json` /
   `launchSettings.json`).

## Scripts & Commands

- **Build:** `dotnet build`
- **Run:** `dotnet run --project Backend/src/GoGameShop.Api`
- **Add Migration:** `dotnet ef migrations add <MigrationName> --project Backend/src/GoGameShop.Api`
- **Update Database:** `dotnet ef database update --project Backend/src/GoGameShop.Api`

## Environment Variables & Configuration

Configuration is managed via `appsettings.json` and `appsettings.Development.json`.

- **ConnectionStrings:GoGameShop:** SQLite connection string — defined in `appsettings.Development.json` (Default:
  `Data Source=GoGameShop.db`).
- **Logging:** Log levels are configured per namespace. EF Core SQL command logging is set to `Warning` in development
  to suppress verbose query output.
- **Authentication:Schemes:Keycloak:** JWT bearer options for the named `Keycloak` scheme. `Authority` is the Keycloak
  realm URL and `ValidAudience` is the expected `aud` claim (the Keycloak client ID). .NET 8+ binds these onto
  `JwtBearerOptions` automatically — no `builder.Configuration.Bind(...)` glue needed.

## API Endpoints

### Games

| Method   | Route         | Description                                                                                                         |
|----------|---------------|---------------------------------------------------------------------------------------------------------------------|
| `GET`    | `/games`      | Get a paginated, searchable list of games (supports `pageNumber`, `pageSize`, and `Name` query params)              |
| `GET`    | `/games/{id}` | Get a single game by ID                                                                                             |
| `POST`   | `/games`      | Create a new game (accepts `multipart/form-data`; optional `ImageFile` field) — requires `AdminAccess` policy       |
| `PUT`    | `/games/{id}` | Update an existing game (accepts `multipart/form-data`; optional `ImageFile` field) — requires `AdminAccess` policy |
| `DELETE` | `/games/{id}` | Delete a game by ID — requires `AdminAccess` policy                                                                 |

### Baskets

| Method | Route               | Description                                                                                                                               |
|--------|---------------------|-------------------------------------------------------------------------------------------------------------------------------------------|
| `GET`  | `/baskets/{userId}` | Get a customer's basket with items and total amount. Returns an empty basket if none exists — requires `UserAccess` policy (via fallback) |
| `PUT`  | `/baskets/{userId}` | Create or replace a customer's basket (upsert) — requires `UserAccess` policy (via fallback)                                              |

### Genres & Ratings

| Method | Route      | Description     |
|--------|------------|-----------------|
| `GET`  | `/genres`  | Get all genres  |
| `GET`  | `/ratings` | Get all ratings |

## Project Structure

```text
GoGameShop/
├── Backend/
│   ├── localinfra/                       # Local development infrastructure
│   │   ├── docker-compose.yml            # Keycloak container definition
│   │   └── gogameshop-realm.json         # Keycloak realm export (roles, clients, settings)
│   ├── src/
│   │   └── GoGameShop.Api/               # Main API project
│   │       ├── Data/                     # EF Core DbContext, seeding, and migrations
│   │       │   ├── Migrations/           # Auto-generated EF Core migration files
│   │       │   ├── DataExtensions.cs     # DB initialization and seed data logic
│   │       │   ├── GoGameShopContext.cs  # EF Core DbContext
│   │       │   └── GoGameShopData.cs     # Hardcoded sample data (commented out, kept for reference)
│   │       ├── Shared/                   # Cross-cutting concerns
│   │       │   ├── Authorization/        # Policies, Roles, ClaimTypes, Schemes, AuthorizationExtensions (Add* extension methods), and KeycloakClaimsTransformer (splits the OAuth `scope` string into per-scope claims)
│   │       │   ├── ErrorHandling/        # GlobalErrorHandler (IExceptionHandler)
│   │       │   ├── FileUpload/           # FileUploader service and FileUploadResult
│   │       │   └── Timing/               # RequestTimingMiddleware (custom middleware, kept for reference)
│   │       ├── Features/                 # Vertical slices by feature
│   │       │   ├── Games/
│   │       │   │   ├── Constants/        # Endpoint name constants
│   │       │   │   ├── CreateGame/       # POST /games (endpoint + DTOs)
│   │       │   │   ├── DeleteGame/       # DELETE /games/{id} (endpoint)
│   │       │   │   ├── GetGame/          # GET /games/{id} (endpoint + DTOs)
│   │       │   │   ├── GetGames/         # GET /games (endpoint + DTOs)
│   │       │   │   ├── UpdateGame/       # PUT /games/{id} (endpoint + DTOs)
│   │       │   │   └── GamesEndpoints.cs # Maps all /games routes
│   │       │   ├── Baskets/
│   │       │   │   ├── Authorization/    # BasketAuthorizationHandler (resource-based auth)
│   │       │   │   ├── GetBasket/        # GET /baskets/{userId} (endpoint + DTOs)
│   │       │   │   ├── UpsertBasket/     # PUT /baskets/{userId} (endpoint + DTOs)
│   │       │   │   └── BasketEndpoints.cs # Maps all /baskets routes
│   │       │   ├── Genres/               # GET /genres (endpoint + DTOs)
│   │       │   └── Ratings/              # GET /ratings (endpoint + DTOs)
│   │       ├── Models/                   # Domain entities (Game, Genre, Rating)
│   │       ├── Properties/               # launchSettings.json
│   │       ├── GlobalUsings.cs           # Global namespace imports
│   │       ├── appsettings.json          # App configuration
│   │       ├── appsettings.Development.json
│   │       └── Program.cs                # Application entry point
│   └── gogameshop.http                   # Sample HTTP requests for testing
├── postman/                              # Postman workspace
│   ├── collections/                      # Saved API collections
│   ├── environments/                     # Environment variables
│   ├── flows/
│   ├── globals/
│   ├── mocks/
│   └── specs/
├── .postman/                             # Postman backup/config
├── LICENSE                               # MIT License
└── README.md                             # Project documentation
```

## Tests

- **TODO:** Implement automated unit and integration tests (e.g., using xUnit or NUnit).
- **Manual Testing (HTTP file):** Use `Backend/gogameshop.http` with
  the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension in VS Code or the
  built-in HTTP client in JetBrains Rider.
- **Manual Testing (Postman):** Import collections from the `postman/collections/` directory into Postman.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Roadmap

```
▓▓▓▓▓▓▓▓▓▓  Vertical Slice Architecture, REST API, CRUD, DTOs, EF Core, SQLite
▓▓▓▓▓▓▓▓▓▓  Middleware, HTTP Logging, Error Handling, Pagination, Search, File System, OpenAPI
▓▓▓▓▓▓▓░░░  JWT Authentication, Role & Policy Authorization, Keycloak, OAuth 2.0, OpenID Connect
░░░░░░░░░░  Azure App Service, PostgreSQL, Blob Storage, Key Vault, CDN, CI/CD Pipelines
░░░░░░░░░░  Containerization, .NET Aspire, Health Checks, Azure Container Apps
░░░░░░░░░░  Background Workers, Service Bus, Outbox Pattern, Stripe Payments
░░░░░░░░░░  Integration Testing
```
