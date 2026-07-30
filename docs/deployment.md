# Deployment

## Required environment variables

All configuration is supplied via environment variables prefixed with `FIXTURELY_` (double
underscore `__` maps to nested configuration sections, matching ASP.NET Core's standard
environment-variable configuration provider convention).

| Variable | Required | Example / notes |
|----------|----------|-------------------|
| `FIXTURELY_CONNECTIONSTRINGS__DEFAULTCONNECTION` | yes | SQL Server connection string. |
| `FIXTURELY_REDIS__CONNECTIONSTRING` | yes | Redis connection string (e.g. `redis-host:6379,password=...`). |
| `FIXTURELY_JWT__ISSUER` | yes | JWT `iss` claim value. |
| `FIXTURELY_JWT__AUDIENCE` | yes | JWT `aud` claim value. |
| `FIXTURELY_JWT__SIGNINGKEY` | yes | **Secret.** ≥ 32 random characters. Rotate carefully — rotating invalidates all outstanding access tokens. |
| `FIXTURELY_JWT__ACCESSTOKENMINUTES` | no (default `15`) | Access token lifetime. |
| `FIXTURELY_JWT__REFRESHTOKENDAYS` | no (default `7`) | Refresh token lifetime. |
| `FIXTURELY_SESSION__IDLETIMEOUTMINUTES` | no (default `15`) | Redis sliding session idle timeout. |
| `FIXTURELY_FRONTEND__BASEURL` | yes | Used for CORS origin and all email links (confirmation/reset/invitation). Must be the exact frontend origin, no trailing slash. |
| `FIXTURELY_SMTP__HOST` | yes | `smtp-relay.brevo.com` in production. |
| `FIXTURELY_SMTP__PORT` | yes | `587` for Brevo. |
| `FIXTURELY_SMTP__USERNAME` | yes (prod) | Brevo SMTP login. **Secret.** |
| `FIXTURELY_SMTP__PASSWORD` | yes (prod) | Brevo SMTP key. **Secret.** |
| `FIXTURELY_SMTP__FROMEMAIL` | yes | Verified sender address in Brevo. |
| `FIXTURELY_SMTP__FROMNAME` | no (default `Fixturely`) | Display name for outgoing email. |
| `FIXTURELY_SMTP__USESSL` | no (default `false`) | `false` for Brevo port 587 (STARTTLS is negotiated automatically); set `true` only for an implicit-TLS SMTP port. |
| `AutoMigrate` | no | Set to `true` to apply pending EF Core migrations automatically on startup in non-Development environments where this isn't already the default. |

**Never commit real values for any secret-marked variable.** Supply them through your platform's
secret manager (Kubernetes Secrets, Azure Key Vault + App Configuration, AWS Secrets Manager,
Docker Swarm secrets, etc.) — never bake them into an image layer or a committed `.env` file.

## Database migrations in production

The API applies pending EF Core migrations automatically on startup **unless** running in the
`Development` or `Testing` ASP.NET Core environment (see `Program.cs`). For a controlled
production rollout, it is generally preferable to run migrations as an explicit pre-deployment
step rather than relying on automatic migration inside the running container:

```bash
dotnet ef database update \
  --project src/Fixturely.Infrastructure/Fixturely.Infrastructure.csproj \
  --startup-project src/Fixturely.Api/Fixturely.Api.csproj \
  --connection "$FIXTURELY_CONNECTIONSTRINGS__DEFAULTCONNECTION"
```

Run this from a CI/CD job or a one-off migration container with network access to the production
database, using the same connection string the API will use at runtime.

## Brevo SMTP setup

1. Create/verify a sender domain or sender email in Brevo.
2. Generate an SMTP key from Brevo's SMTP & API settings.
3. Set:
   ```bash
   FIXTURELY_SMTP__HOST=smtp-relay.brevo.com
   FIXTURELY_SMTP__PORT=587
   FIXTURELY_SMTP__USERNAME=<brevo-smtp-login>
   FIXTURELY_SMTP__PASSWORD=<brevo-smtp-key>
   FIXTURELY_SMTP__FROMEMAIL=<verified-sender@yourdomain.com>
   FIXTURELY_SMTP__USESSL=false
   ```
4. The `smtp-configuration` readiness health check (`/health/ready`) verifies that host, port, and
   from-email are non-empty and well-formed at startup — it does **not** attempt a live SMTP
   handshake, so a wrong password will surface as an email-delivery failure recorded in
   `EmailDeliveryEvent`, not as a readiness failure.

Because `BrevoSmtpEmailSender` only uses standard MailKit SMTP, switching providers later
(SendGrid, Amazon SES SMTP interface, Postmark, your own Postfix relay, etc.) requires only
updating these environment variables — no code or redeploy-of-image changes.

## Container image

Build and push the production image:

```bash
docker build -t <registry>/fixturely-api:<tag> .
docker push <registry>/fixturely-api:<tag>
```

The image:

- Uses the official `mcr.microsoft.com/dotnet/sdk:8.0` build stage and
  `mcr.microsoft.com/dotnet/aspnet:8.0` runtime stage (multi-stage build — the final image
  contains no SDK, no source code beyond published binaries).
- Runs as a **non-root** user (`fixturely`, uid `5678`).
- Listens on port `8080` (`ASPNETCORE_URLS=http://+:8080`); put a reverse proxy or load balancer
  in front for TLS termination in production, or configure Kestrel for HTTPS directly if the
  container terminates TLS itself.

## Health checks for orchestration

| Endpoint | Purpose | Suitable for |
|----------|---------|---------------|
| `/health` | Aggregate of every registered check | General monitoring |
| `/health/live` | Always healthy once the process is up (no dependency checks) | Kubernetes `livenessProbe` |
| `/health/ready` | SQL Server connectivity, Redis connectivity, SMTP configuration validity | Kubernetes `readinessProbe` / load balancer health check |

## Security checklist before going live

- [ ] `FIXTURELY_JWT__SIGNINGKEY` is a unique, random, ≥32-character secret per environment.
- [ ] `FIXTURELY_FRONTEND__BASEURL` is the exact production frontend origin (CORS is not
      wildcarded).
- [ ] TLS is terminated somewhere in front of the API (reverse proxy, load balancer, or Kestrel
      itself) so `Secure` cookies and HSTS are meaningful.
- [ ] SQL Server and Redis are not exposed to the public internet; only the API reaches them.
- [ ] Brevo (or chosen SMTP provider) sender domain has SPF/DKIM configured to avoid landing in
      spam, which would otherwise silently break confirmation/reset/invitation flows for real
      users.
- [ ] Structured logs (Serilog) are shipped to a log aggregator; confirm no secret values ever
      appear in log output (the codebase never logs passwords, tokens, or connection strings by
      design — verify this holds after any future change).
