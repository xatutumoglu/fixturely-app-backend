# Fixturely Backend

Backend API for **Fixturely**, a football (soccer) tournament management platform. Fixturely lets
organizers create private tournaments, invite collaborators with scoped roles, generate fixtures
for several supported formats, enter match results, and track standings and knockout brackets in
real time.

## Repository boundary

This repository (`fixturely-app-backend`) contains **only** the backend API. It intentionally
contains no frontend source code — no React, no TypeScript, no HTML/CSS intended as UI, no
frontend build tooling. The Fixturely React frontend is developed independently in the
`fixturely-app-frontend` repository once this backend's API contract is finalized. The backend
exposes a stable, versioned, documented REST API (`/api/v1/...`) that the frontend consumes over
HTTP.

## Technology stack

- **C# / .NET 8 LTS**, ASP.NET Core Web API
- **Entity Framework Core** with **Microsoft SQL Server**
- **ASP.NET Core Identity** (custom `ApplicationUser`) for authentication
- **JWT** access tokens + rotating **refresh tokens** (HttpOnly cookie)
- **Redis** for session state, rate limiting support, and optional caching
- **MailKit** for SMTP email delivery (Brevo in production, Mailpit locally)
- **FluentValidation** for request validation
- **Serilog** for structured logging
- **Swagger / OpenAPI** (Swashbuckle) for API documentation
- **ASP.NET Core Health Checks**
- **OpenTelemetry** for tracing (HTTP, EF Core/SQL, Redis)
- **Docker** / **Docker Compose** for local development and deployment
- **xUnit**, **FluentAssertions**, **Moq**, **Testcontainers** for testing
- **GitHub Actions** for CI

## Architecture summary

The solution follows **Clean Architecture** with a strict, one-directional dependency graph:

```
Fixturely.Domain           <- no dependencies
Fixturely.Application      <- depends on Domain
Fixturely.Infrastructure   <- depends on Application, Domain
Fixturely.Api               <- depends on Application, Infrastructure, Domain
```

- **Fixturely.Domain** — entities, value objects, domain enums, and domain exceptions. All
  business rules that belong to a single aggregate (e.g. `Tournament`, `Match`) live here.
- **Fixturely.Application** — use-case orchestration: application services, DTOs,
  FluentValidation validators, abstractions (`IApplicationDbContext`, `IEmailSender`,
  `ISessionStore`, `ITokenService`, `ITournamentAuthorizationService`, etc.) and the
  strategy-based tournament format engines (`ITournamentFormatEngine`).
- **Fixturely.Infrastructure** — EF Core `ApplicationDbContext` and migrations, ASP.NET Core
  Identity wiring, Redis-backed cache/session store, SMTP email sending, JWT token generation.
- **Fixturely.Api** — controllers, middleware (correlation id, exception handling, security
  headers, session validation), Swagger, health checks, OpenTelemetry, and the composition root
  (`Program.cs`).

See [docs/architecture.md](docs/architecture.md) for a deeper description of module boundaries
and the tournament format engine design.

## Local setup

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (for SQL Server, Redis,
  Mailpit, and integration tests via Testcontainers)
- `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

### Environment variables

Copy `.env.example` to `.env` and adjust values as needed:

```bash
cp .env.example .env
```

All configuration keys use the `FIXTURELY_` environment variable prefix (mapped from
`Section__Key` style, e.g. `FIXTURELY_JWT__SIGNINGKEY`). See [docs/local-development.md](docs/local-development.md)
for the full list and [docs/deployment.md](docs/deployment.md) for production guidance.

**Never commit real secrets.** `appsettings.json` only contains non-secret defaults;
`appsettings.Development.json` contains local-development-safe defaults for running outside
Docker (e.g. from an IDE). Production secrets must be supplied through environment variables or a
secret manager.

## Docker Compose usage

Start the full stack (API + SQL Server + Redis + Mailpit):

```bash
docker compose up -d --build
```

Services:

| Service               | Purpose                          | Port(s)          |
|------------------------|-----------------------------------|-------------------|
| `fixturely-api`        | Backend API                       | `8080`            |
| `fixturely-sqlserver`  | SQL Server 2022                   | `1433`            |
| `fixturely-redis`      | Redis 7                           | `6379`            |
| `fixturely-mailpit`    | SMTP test server + web UI         | `1025` (SMTP), `8025` (Web UI) |

Stop and remove everything (including volumes):

```bash
docker compose down -v
```

### SQL Server, Redis, and Mailpit access

- **SQL Server**: `Server=localhost,1433;User Id=sa;Password=<FIXTURELY_SQL_SA_PASSWORD>;`
- **Redis**: `localhost:6379`
- **Mailpit UI**: http://localhost:8025 (every email sent by the API in local development lands
  here — nothing is delivered externally)

## EF Core migrations

Migrations live in `src/Fixturely.Infrastructure/Persistence/Migrations`. To add a new migration:

```bash
dotnet ef migrations add <MigrationName> \
  --project src/Fixturely.Infrastructure/Fixturely.Infrastructure.csproj \
  --startup-project src/Fixturely.Api/Fixturely.Api.csproj \
  --output-dir Persistence/Migrations
```

To apply migrations manually (the API also applies them automatically on startup outside of the
`Development`/`Testing` environments, or when `AutoMigrate=true`):

```bash
dotnet ef database update \
  --project src/Fixturely.Infrastructure/Fixturely.Infrastructure.csproj \
  --startup-project src/Fixturely.Api/Fixturely.Api.csproj
```

## Swagger

Once the API is running, Swagger UI is available at:

```
http://localhost:8080/swagger
```

(Swagger UI is enabled only in the `Development` environment; use the `/swagger/v1/swagger.json`
document directly in other environments if needed for tooling.)

## Running tests

```bash
# Unit tests (no external dependencies)
dotnet test tests/Fixturely.UnitTests/Fixturely.UnitTests.csproj

# Integration tests (requires Docker — spins up SQL Server + Redis via Testcontainers)
dotnet test tests/Fixturely.IntegrationTests/Fixturely.IntegrationTests.csproj

# Everything
dotnet test Fixturely.sln
```

## Brevo SMTP production configuration

Production email delivery uses [Brevo](https://www.brevo.com/)'s standard SMTP relay through the
provider-agnostic `IEmailSender` abstraction (implemented by `BrevoSmtpEmailSender`, which uses
plain MailKit SMTP — no Brevo-specific SDK). Configure via environment variables:

```bash
FIXTURELY_SMTP__HOST=smtp-relay.brevo.com
FIXTURELY_SMTP__PORT=587
FIXTURELY_SMTP__USERNAME=<your-brevo-smtp-login>
FIXTURELY_SMTP__PASSWORD=<your-brevo-smtp-key>
FIXTURELY_SMTP__FROMEMAIL=no-reply@yourdomain.com
FIXTURELY_SMTP__FROMNAME=Fixturely
FIXTURELY_SMTP__USESSL=false
```

Because the implementation only relies on standard SMTP, swapping to any other standards-compliant
provider requires configuration changes only — no code changes.

## Security overview

- Passwords hashed exclusively through ASP.NET Core Identity; never logged, never emailed.
- Mandatory email confirmation before login; account-enumeration-safe forgot-password/resend flows.
- JWT access tokens (default 15 min) + rotating, single-use, hashed refresh tokens (default 7 days)
  delivered via secure `HttpOnly` cookie.
- Redis-backed sessions with a sliding idle timeout (default 15 min); every authorized request
  revalidates session freshness independent of JWT expiry.
- Tournament-scoped roles (`Owner`, `ScoreManager`, `Viewer`) enforced through resource-based
  authorization on every tournament-scoped endpoint — never by trusting client-supplied IDs alone.
- Rate limiting on registration, login, password reset, confirmation resend, and invitation
  endpoints.
- RFC 7807 `ProblemDetails` error responses; optimistic concurrency (`RowVersion`) on mutable
  entities returns `409 Conflict` on stale writes.
- Security headers (`X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, HSTS in
  production), explicit CORS restricted to `FIXTURELY_FRONTEND__BASEURL` (no wildcard in
  production).

See [docs/authentication.md](docs/authentication.md) for full detail.

## Deployment notes

See [docs/deployment.md](docs/deployment.md) for production deployment guidance, including
required environment variables, database migration strategy, and Brevo SMTP setup.

## Documentation index

- [docs/architecture.md](docs/architecture.md) — Clean Architecture boundaries and format engine design
- [docs/api-contract.md](docs/api-contract.md) — REST API surface
- [docs/authentication.md](docs/authentication.md) — auth, sessions, tokens
- [docs/tournament-rules.md](docs/tournament-rules.md) — formats, standings, tie-breaks, brackets
- [docs/local-development.md](docs/local-development.md) — local dev setup in detail
- [docs/deployment.md](docs/deployment.md) — production deployment
- [docs/assumptions.md](docs/assumptions.md) — documented assumptions and excluded scope
