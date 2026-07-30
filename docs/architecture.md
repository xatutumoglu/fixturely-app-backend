# Architecture

Fixturely's backend follows **Clean Architecture** with a strict, unidirectional dependency graph
enforced by project references:

```
Fixturely.Domain
  ^
  |
Fixturely.Application
  ^
  |
Fixturely.Infrastructure
  ^
  |
Fixturely.Api
```

`Fixturely.Domain` has no project references. `Fixturely.Application` references only `Domain`.
`Fixturely.Infrastructure` references `Application` and `Domain`. `Fixturely.Api` references all
three. This is enforced at compile time by the `.csproj` `ProjectReference` entries — there is no
way for `Domain` to accidentally depend on EF Core, ASP.NET Core, or any infrastructure concern.

## Fixturely.Domain

Contains:

- **Entities** (`src/Fixturely.Domain/Entities`) — `Tournament`, `Participant`, `TournamentMember`,
  `TournamentGroup`/`GroupParticipant`, `TournamentRound`, `Match`, `FixtureGenerationHistory`,
  `TournamentInvitation`, `RefreshToken`, `UserSession`, `EmailDeliveryEvent`, `AuditLog`,
  `TieBreakResolution`. All entities are private-setter, factory-method-constructed, and expose
  only behavior methods that preserve invariants (e.g. `Tournament.AddParticipant` throws
  `ParticipantAlreadyExistsException` on duplicate names; `Match.CompleteWithWinner` throws
  `InvalidScoreException` if a knockout match has no determinable winner).
- **Enums** — `TournamentStatus`, `TournamentFormat`, `LegMode`, `TournamentMemberRole`,
  `MatchStatus`, `TournamentMemberStatus`, `InvitationStatus`, `EmailTemplateType`, etc.
- **Domain exceptions** (`src/Fixturely.Domain/Exceptions`) — typed exceptions
  (`TournamentNotFoundException`, `UnauthorizedTournamentAccessException`,
  `InvalidTournamentStateException`, `InvalidScoreException`, `ConcurrencyConflictException`,
  `TournamentGroupCompositionException`, `KnockoutPairingException`, `InvalidCredentialsException`,
  etc.) mapped to RFC 7807 `ProblemDetails` responses by the API's exception-handling middleware.
- **Common** (`src/Fixturely.Domain/Common`) — `Entity`/`SoftDeletableEntity` base classes
  providing `Id`, `CreatedAtUtc`, `UpdatedAtUtc`, `RowVersion` (optimistic concurrency token), and
  soft-delete support.

Domain entities never call `DateTime.UtcNow` directly; all timestamps are passed in from the
caller (ultimately sourced from an injected `TimeProvider`), which keeps domain logic
unit-testable with fixed clocks.

## Fixturely.Application

Contains:

- **Abstractions** (`Abstractions/Persistence`, `Abstractions/Identity`, `Abstractions/Email`,
  `Abstractions/Caching`, `Abstractions/Security`) — interfaces implemented by Infrastructure:
  `IApplicationDbContext`, `IIdentityService`, `IEmailSender`/`IEmailNotificationService`,
  `ICacheService`/`ISessionStore`, `ITokenService`/`ICurrentUserService`/`ITournamentAuthorizationService`.
- **DTOs** (`DTOs/Auth`, `DTOs/Tournaments`, `DTOs/Participants`, `DTOs/Members`, `DTOs/Matches`,
  `DTOs/Common`) — request/response records used by controllers; entities never leak past the
  Application layer boundary.
- **Validators** (`Validators/`) — FluentValidation validators for every request DTO, auto-run by
  `Fixturely.Api.Filters.ValidationActionFilter` before controller actions execute.
- **Auth** (`Auth/AuthService.cs`) — registration, confirmation, login, refresh rotation, logout,
  logout-all, forgot/reset password.
- **Tournaments** — the bulk of business orchestration:
  - `TournamentService`, `ParticipantService` — CRUD with resource-based authorization.
  - `Tournaments/Formats` — the **strategy-based tournament format engine** (see below).
  - `Tournaments/Fixtures/FixtureGenerationService` — orchestrates format engine invocation,
    persistence, fixture-generation history/audit, and regeneration rules.
  - `Tournaments/Matches/MatchService` — score entry, knockout tie resolution, bracket
    propagation, dependent-match invalidation on historical score correction.
  - `Tournaments/Matches/QualificationService` — resolves group winners/runners-up into pending
    knockout qualifier slots once a group's matches are complete.
  - `Tournaments/Standings` — `StandingsCalculationService`, `TieBreakerService` (recursive
    mini-table tie-break resolution), `TournamentQueryService` (standings/bracket/rounds/audit-log
    read models).
  - `Tournaments/Bracket/BracketProgressionService` — single-leg and double-leg tie decision logic
    (extra time, penalties, aggregate score with no away-goals rule) and downstream-match discovery
    for score-correction invalidation.
  - `Tournaments/Members/MembershipService` — invitations (create/resend/revoke/accept), member
    role changes, member removal.
  - `Tournaments/TournamentAuthorizationService` — resource-based tournament-scoped role checks
    (`EnsureCanViewAsync`, `EnsureCanManageScoresAsync`, `EnsureIsOwnerAsync`).

### Tournament format engine design

`ITournamentFormatEngine` is the strategy interface:

```csharp
public interface ITournamentFormatEngine
{
    TournamentFormat Format { get; }
    FixtureGenerationOutput GenerateFixture(FixtureGenerationInput input);
}
```

Four focused implementations are registered in DI and resolved by `TournamentFormat`:

- `LeagueFormatEngine` — round-robin (single/double leg) via `RoundRobinScheduler` (circle method).
- `GroupStageFormatEngine` — draws groups of exactly four via `GroupDrawHelper` (secure shuffle),
  then round-robins each group.
- `KnockoutFormatEngine` — builds the full bracket (not just round 1) via `BracketSeedOrderCalculator`
  (standard seeding order that spreads BYEs) and `KnockoutRoundBuilder` (shared round-2-onward
  bracket construction, including the optional third-place match).
- `GroupKnockoutFormatEngine` — runs the group stage, then builds a first knockout round with
  **unresolved qualifier references** (`Match.HomeQualifierGroupOrderIndex`/`Position`) that are
  only resolved once `QualificationService` determines each group's top two, guaranteeing
  same-group participants never meet in the first knockout round.

There is deliberately no giant `switch` statement anywhere in this pipeline — `FixtureGenerationService`
looks up the correct engine from a `Dictionary<TournamentFormat, ITournamentFormatEngine>` built
from the DI-registered collection. Adding a fifth format means adding one new
`ITournamentFormatEngine` implementation and registering it; no existing code changes.

## Fixturely.Infrastructure

- **Persistence** — `ApplicationDbContext` (implements `IApplicationDbContext`, extends
  `IdentityUserContext<ApplicationUser, Guid>`), EF Core entity configurations
  (`Persistence/Configurations`), and migrations (`Persistence/Migrations`).
- **Identity** — `ApplicationUser` (custom Identity user with `IsActive`, audit timestamps) and
  `IdentityService` (implements `IIdentityService` over `UserManager<ApplicationUser>`).
- **Caching** — `RedisCacheService` (`ICacheService`) and `RedisSessionStore` (`ISessionStore`,
  implements the `fixturely:session:{sessionId}` sliding-idle-timeout session pattern).
- **Email** — `BrevoSmtpEmailSender` (`IEmailSender`, plain MailKit SMTP — Brevo-agnostic) and
  `EmailNotificationService` (`IEmailNotificationService`, builds templated messages and records
  `EmailDeliveryEvent` audit rows without ever storing secret token values).
- **Security** — `JwtTokenService` (`ITokenService`).
- **DependencyInjection.cs** — registers EF Core, Identity, Redis, JWT/SMTP/Frontend/Session
  options binding, and all of the above services.

## Fixturely.Api

- **Controllers** — thin, one responsibility per tournament sub-resource
  (`AuthController`, `TournamentsController`, `ParticipantsController`, `MembersController`,
  `MatchesController`, `InvitationsController`). Controllers never contain business logic — they
  extract the current user id from claims, call an Application service, and map the result to an
  HTTP response.
- **Middleware** — `CorrelationIdMiddleware` (adds/propagates `X-Correlation-Id`, pushes it into
  Serilog's `LogContext`), `ExceptionHandlingMiddleware` (maps domain/validation exceptions to RFC
  7807 `ProblemDetails` with correct status codes), `SecurityHeadersMiddleware`,
  `SessionValidationMiddleware` (enforces the Redis sliding-idle-timeout on every endpoint that
  requires authorization, while leaving anonymous endpoints untouched).
- **Security** — `CurrentUserService` (`ICurrentUserService`, wraps `IHttpContextAccessor`).
- **Extensions/ApiServiceCollectionExtensions.cs** — JWT bearer auth, CORS (single explicit origin
  from `Frontend:BaseUrl`, no wildcard), rate limiting policies, Swagger/OpenAPI with Bearer
  security scheme, health checks (SQL Server via `AddDbContextCheck`, Redis, SMTP configuration
  validity).
- **Program.cs** — composition root: Serilog bootstrap, environment variable configuration
  (`FIXTURELY_` prefix), OpenTelemetry tracing, middleware pipeline ordering, health check
  endpoints (`/health`, `/health/live`, `/health/ready`), and automatic migration on startup
  outside `Development`/`Testing`.

## Cross-cutting concerns

- **Concurrency**: every mutable entity has a `RowVersion` (SQL Server `rowversion`/timestamp)
  column. Application services call `IApplicationDbContext.SetOriginalRowVersion(entity, clientRowVersion)`
  before mutating, so a stale write throws `DbUpdateConcurrencyException`, translated by
  `TournamentService`/`MatchService` into `ConcurrencyConflictException` → HTTP 409.
- **Transactions**: multi-row-affecting operations (fixture generation/regeneration, score updates
  that cascade bracket progression) are wrapped so SaveChanges commits atomically; the retrying
  SQL Server execution strategy (`EnableRetryOnFailure`) is respected — no manual
  `BeginTransaction`/`Commit` pairs are used, since EF Core's own `SaveChangesAsync` already wraps
  each call in an implicit transaction compatible with the retrying strategy.
- **Time abstraction**: `TimeProvider` (registered as `TimeProvider.System` in production, a fake
  in unit tests) is injected everywhere instead of calling `DateTime.UtcNow` directly, keeping
  business logic testable with deterministic clocks.
- **Auditing**: `AuditLog` rows are written for tournament creation/update/archive, participant
  changes, fixture generation/regeneration/confirmation, invitation lifecycle events, and score
  entry/correction/invalidation — all queryable via `GET /api/v1/tournaments/{id}/audit-logs`.
