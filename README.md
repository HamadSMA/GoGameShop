# GoGameShop (In Progress)

A production-ready game store API for selling digital game keys. Built with the modern .NET stack, focusing on clean architecture and minimal API patterns.

---

> **AI Disclosure** — Source code is 100% handwritten. Commit messages are AI-generated. Docs and notes are collaborative — AI-assisted, personally reviewed, and edited.

📓 **Dev Notes** — Running notes I take while building this project. [View notes →](./NOTES.md)

---

## Overview

GoGameShop is a backend service designed to manage a catalog of digital games, including features for browsing genres, ratings, and managing game details (CRUD operations). It leverages the latest .NET features to provide a performant and scalable foundation.

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

## Setup & Run

1.  **Clone the repository:**
    ```bash
    git clone <repository-url>
    cd GoGameShop
    ```

2.  **Restore dependencies:**
    ```bash
    dotnet restore
    ```

3.  **Apply Database Migrations:**
    The application uses SQLite. To set up the initial database:
    ```bash
    dotnet ef database update --project Backend/src/GoGameShop.Api
    ```
    *Note: The application is configured to run `app.MigrateDb()` on startup, which should handle migrations automatically.*

4.  **Run the application:**
    ```bash
    dotnet run --project Backend/src/GoGameShop.Api
    ```
    The API will be available at `http://localhost:5078` (or the port specified in `appsettings.json` / `launchSettings.json`).

## Scripts & Commands

- **Build:** `dotnet build`
- **Run:** `dotnet run --project Backend/src/GoGameShop.Api`
- **Add Migration:** `dotnet ef migrations add <MigrationName> --project Backend/src/GoGameShop.Api`
- **Update Database:** `dotnet ef database update --project Backend/src/GoGameShop.Api`

## Environment Variables & Configuration

Configuration is managed via `appsettings.json` and `appsettings.Development.json`.

- **ConnectionStrings:GoGameShop:** SQLite connection string (Default: `Data Source=GoGameShop.db`).

## Project Structure

```text
GoGameShop/
├── Backend/
│   ├── src/
│   │   └── GoGameShop.Api/       # Main API Project
│   │       ├── Data/             # EF Core DbContext and Migrations
│   │       ├── Features/         # Vertical slices for API endpoints (Games, Genres, etc.)
│   │       ├── Models/           # Domain models/Entities
│   │       └── Program.cs        # Application entry point
│   └── gogameshop.http           # Sample HTTP requests for testing
├── LICENSE                       # MIT License
└── README.md                     # Project documentation
```

## Tests

- **TODO:** Implement automated unit and integration tests (e.g., using xUnit or NUnit).
- **Manual Testing:** Use the `Backend/gogameshop.http` file with the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension in VS Code or the built-in HTTP client in JetBrains Rider.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Roadmap

The project is actively under development. Planned features include:

- **Security:** JWT Authentication and HTTPS hardening.
- **Frontend:** Responsive Frontend development.
- **Payments:** Stripe integration for secure game key purchases.
- **Infrastructure:** PostgreSQL migration, Containerization (Docker), and Azure Cloud deployment.
- **Architecture:** Middleware integration and Background Services.
- **CI/CD:** Automated deployment pipelines.
- **Testing:** Comprehensive Integration testing.
