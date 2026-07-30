# Local Development

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- `dotnet-ef` CLI tool:

  ```bash
  dotnet tool install --global dotnet-ef
  ```

## Option A — run everything in Docker Compose (recommended)

```bash
cp .env.example .env    # adjust values if desired
docker compose up -d --build
```

This starts:

- `fixturely-api` on `http://localhost:8080` (Swagger at `http://localhost:8080/swagger`)
- `fixturely-sqlserver` on `localhost,1433`
- `fixturely-redis` on `localhost:6379`
- `fixturely-mailpit` — SMTP on `localhost:1025`, web UI on `http://localhost:8025`

The API container applies EF Core migrations automatically on startup (`AutoMigrate=true` is set
in `docker-compose.yml`).

To view logs:

```bash
docker compose logs -f fixturely-api
```

To stop and remove containers (keeping data volumes):

```bash
docker compose down
```

To stop and remove everything including data volumes:

```bash
docker compose down -v
```

## Option B — run the API from your IDE against Dockerized dependencies

Start only the dependency containers:

```bash
docker compose up -d fixturely-sqlserver fixturely-redis fixturely-mailpit
```

Then run the API from your IDE or via:

```bash
dotnet run --project src/Fixturely.Api/Fixturely.Api.csproj
```

`appsettings.Development.json` already points at `localhost` for SQL Server, Redis, and Mailpit,
matching the ports exposed by `docker-compose.yml`. Apply migrations manually the first time (or
whenever a new migration is added):

```bash
dotnet ef database update \
  --project src/Fixturely.Infrastructure/Fixturely.Infrastructure.csproj \
  --startup-project src/Fixturely.Api/Fixturely.Api.csproj
```

## Connection details

| Dependency | Host/Port (from your machine) | Credentials |
|------------|-------------------------------|--------------|
| SQL Server | `localhost,1433` | `sa` / value of `FIXTURELY_SQL_SA_PASSWORD` (`.env`, default `Fixturely_Dev_2024!`) |
| Redis      | `localhost:6379` | none |
| Mailpit SMTP | `localhost:1025` | none (no auth in local dev) |
| Mailpit Web UI | `http://localhost:8025` | none |

**Using Mailpit**: every email the API sends locally (confirmation, password reset, invitations)
is delivered to Mailpit instead of a real inbox. Open `http://localhost:8025` to view/inspect any
email sent by the running API — click through to grab confirmation/reset links during manual
testing.

## Swagger

`http://localhost:8080/swagger` (Docker Compose) or `https://localhost:<port>/swagger` (IDE run,
port shown in the console/`launchSettings.json`).

## EF Core migrations

Add a new migration after changing an entity or entity configuration:

```bash
dotnet ef migrations add <DescriptiveName> \
  --project src/Fixturely.Infrastructure/Fixturely.Infrastructure.csproj \
  --startup-project src/Fixturely.Api/Fixturely.Api.csproj \
  --output-dir Persistence/Migrations
```

Apply pending migrations:

```bash
dotnet ef database update \
  --project src/Fixturely.Infrastructure/Fixturely.Infrastructure.csproj \
  --startup-project src/Fixturely.Api/Fixturely.Api.csproj
```

Remove the most recently added (unapplied) migration:

```bash
dotnet ef migrations remove \
  --project src/Fixturely.Infrastructure/Fixturely.Infrastructure.csproj \
  --startup-project src/Fixturely.Api/Fixturely.Api.csproj
```

The `dotnet ef` commands need a value for `FIXTURELY_JWT__SIGNINGKEY` (any string ≥ 32 chars)
available as an environment variable at design time, since `Program.cs` reads configuration on
startup even for design-time operations. Example:

```bash
FIXTURELY_JWT__SIGNINGKEY="dev-only-signing-key-not-for-production-use-please-change-me-32chars" \
  dotnet ef migrations add MyMigration \
  --project src/Fixturely.Infrastructure/Fixturely.Infrastructure.csproj \
  --startup-project src/Fixturely.Api/Fixturely.Api.csproj
```

## Running tests

```bash
# Unit tests — fast, no external dependencies
dotnet test tests/Fixturely.UnitTests/Fixturely.UnitTests.csproj

# Integration tests — requires Docker running (Testcontainers spins up
# temporary SQL Server + Redis containers automatically)
dotnet test tests/Fixturely.IntegrationTests/Fixturely.IntegrationTests.csproj

# Everything
dotnet test Fixturely.sln
```

Integration tests use a shared `IntegrationTestWebAppFactory` (xUnit collection fixture) that:

- Starts a fresh `mcr.microsoft.com/mssql/server:2022-latest` and `redis:7-alpine` container per
  test run via Testcontainers.
- Applies EF Core migrations against the ephemeral SQL Server container.
- Replaces `IEmailSender` with an in-memory `TestEmailCapture`, so tests can extract
  confirmation/reset/invitation tokens directly from the generated email links without needing a
  real SMTP server.
- Disables the auth/invitation rate limiters (`Testing` environment) so a single test run
  performing many register/login calls back-to-back is never itself rate-limited.

## Formatting and linting

```bash
dotnet format Fixturely.sln --verify-no-changes
```

CI runs this exact command and fails the build if it reports any changes.
