# GoGameShop — Learning Notes

> [!NOTE]
> These are personal notes written alongside the project with AI assistance

---

## Table of Contents

### Language Foundations
- [C# Essentials](notes/csharp-essentials.md): extension methods, dependency injection, records, delegates, generics, exception handling, LINQ
- [Object-Oriented Programming](notes/oop.md): four pillars, SOLID, composition vs inheritance, interfaces, design patterns in ASP.NET Core
- [Async Programming](notes/async-programming.md): async/await, Task, ValueTask, ContinueWith

### Project Structure
- [Project Setup and Configuration](notes/project-setup.md): Program.cs, WebApplication, csproj, appsettings, logging, middleware, options pattern, launchSettings.json
- [Architecture Patterns](notes/architecture-patterns.md): vertical slice, layered, clean, onion, hexagonal, modular monolith, Minimal APIs vs Controllers vs FastEndpoints

### Data
- [Data and EF Core](notes/data-ef-core.md): models, DbContext, migrations, seeding, AsNoTracking, ExecuteDelete, eager loading

### API
- [API Design](notes/api-design.md): route groups, DTOs, validation, CRUD, status codes, Problem Details, OpenAPI, pagination, file uploads

### Security and Identity
- Auth fundamentals
  - [Authentication and Authorization](notes/auth.md): role/claims/policy/resource-based auth, JWT bearer, schemes, fallback policy, claims transformer
  - [OAuth 2.0 and OpenID Connect](notes/oauth-oidc.md): the four roles, grant types, PKCE, client authentication, scopes, JWKS, end-to-end flow
  - [JWT Tokens](notes/jwt-tokens.md): JWT structure, standard claims, Entra claims, `dotnet user-jwts` vs Entra, API-side validation, request walkthrough
- Identity providers
  - [Keycloak](notes/keycloak.md): realms, users, roles, configuring the role and scope claims, realm export and import
  - [Microsoft Entra](notes/entra.md): External ID tenants, app registrations, scopes, Postman client, admin consent, users, app roles, OIDC metadata
- [Secret Management](notes/secret-management.md): configuration providers, user secrets, env vars, container/k8s secrets, Key Vault, App Configuration, managed identity

### Infrastructure
- [Docker](notes/docker.md): images, containers, ports, env vars, volumes, exec into a container, Docker Compose
- [Azure](notes/azure.md): cloud basics, shared responsibility, IaaS/PaaS/SaaS, regions, resource groups, RBAC, hosting options, App Service deploy, logging

### Frontend
- [Blazor Frontend](notes/blazor-frontend.md): Razor components, hosting models, Keycloak integration, cookie session, token refresh, typed HTTP clients, BasketState, AuthorizeView, forms, streaming

### Tooling and Quality
- [Postman](notes/postman.md): workspaces, collections, environments, OAuth 2.0 with Keycloak, testing protected endpoints
- [Testing](notes/testing.md): unit tests, AAA pattern, xUnit, NSubstitute mocking, integration tests, WebApplicationFactory, SQLite in-memory, test auth handlers
