# Assumptions and Excluded Scope

This document records every place where the specification was ambiguous or where a deliberate
scope decision was made, along with the reasoning and the safest reasonable behavior implemented.

## Explicitly excluded from v1 (per specification)

- Swiss system tournaments
- Double-elimination tournaments
- Manual bracket mode
- Public tournaments / public share links (all tournaments are private; access requires an
  accepted membership)
- Team logos / any file uploads
- PDF / Excel export
- Live WebSocket score updates (score changes are visible on next poll/request, not pushed)
- Custom user-defined ranking rules (only the documented deterministic tie-break order is
  implemented)
- .NET Aspire (not used, per explicit instruction)

## Assumptions made due to specification ambiguity

### `Completed` tournament status

The specification describes a `Completed` status ("all mandatory matches are completed") and a
`Reopen` operation that requires status to be `Completed`. The specification does not define the
exact automatic trigger condition for every format (e.g. "every league match played" vs. "the
knockout final has a winner" vs. "every group is finished and the bracket final is decided").

**Assumption**: rather than guess an incorrect automatic-completion heuristic that could mark a
tournament `Completed` prematurely (e.g. before the third-place match is played, which may or may
not be considered "mandatory"), v1 does not automatically transition a tournament to `Completed`.
The status model, `Reopen` domain method, and API endpoint (`POST /tournaments/{id}/reopen`) are
fully implemented and tested, but no automatic trigger sets `Completed` — an Owner-driven explicit
"mark as completed" action is left as a natural, low-risk future addition once the exact
per-format definition of "all mandatory matches" is confirmed with product stakeholders. This is
the safest behavior: a tournament that is never incorrectly locked out from further legitimate
score entry.

### Final tie-break resolution mechanics (tie-break match / mini-league / random draw)

The specification requires that, when standings remain tied after every deterministic criterion,
a "controlled tie-break workflow" produce a tie-break match (2-way ties) or mini-league (3+-way
ties), or — if the domain ultimately requires it — a cryptographically random draw, all
auditable.

**Assumption**: `TieBreakerService` implements the full deterministic ranking order (points →
head-to-head points/goal-difference/goals-scored on the tied subset → overall goal
difference/goals-scored → wins) precisely as specified, and correctly **detects and flags** any
remaining tie via a `TieBreakNote` on the standings response, rather than silently defaulting to
an arbitrary order. The domain entity `TieBreakResolution` and enum
`TieBreakResolutionMethod` (`TieBreakMatch`, `MiniLeague`, `RandomDraw`, etc.) exist and are ready
to record a resolution's method, tied participant ids, and — for `RandomDraw` — the
cryptographically secure seed used. **Actually generating** the tie-break match fixture, the
mini-league fixtures, or executing the random draw via an API endpoint is not implemented in v1,
because the specification does not define the exact competition format for a tie-break
mini-league (single or double leg? does it re-use existing head-to-head results or start fresh?)
or the exact trigger point for when a random draw becomes mandatory vs. an Owner-initiated
tie-break match is preferred. Implementing an incorrect automatic behavior here risked producing
factually wrong sporting outcomes, which is worse than clearly flagging the tie and leaving
resolution to an Owner-driven follow-up action (to be specified with stakeholders and added as a
dedicated `POST /tournaments/{id}/tie-breaks` endpoint in a follow-up iteration).

### Third-place match leg mode

The specification says the third-place match "must follow the selected tournament leg setting
unless a clearly documented tournament rule specifies otherwise" — no override rule is specified.

**Assumption**: the third-place match always uses the tournament's overall `LegMode` (single or
double leg), with no override. This is the literal reading of the specification in the absence of
any documented exception.

### Redis short-lived standings/bracket cache

The specification lists a short-lived standings/backet Redis cache as **optional**.

**Assumption**: v1 does not implement a standings/bracket read-through cache; every read endpoint
queries SQL Server directly. This keeps the read model always consistent with the write model
without needing to reason about cache invalidation races, at the cost of not having the
(optional) performance optimization. Redis is still fully used for its **mandatory** purpose:
session storage with the sliding idle timeout. Adding a cache layer later is a pure performance
optimization that doesn't change any documented behavior in this file.

### Rate limiting exact thresholds

The specification requires rate limiting on register/login/forgot-password/reset-password/resend-confirmation/invitation
endpoints but does not specify exact numeric thresholds.

**Assumption**: a fixed window of 1 minute with a limit of 10 requests (auth-sensitive) / 20
requests (invitation-sensitive) per client IP was chosen as a reasonable, conservative default
that permits legitimate rapid interactive use (e.g. retrying a mistyped password) while still
meaningfully throttling automated abuse. These limits are configured in
`Fixturely.Api.Extensions.ApiServiceCollectionExtensions.AddFixturelyApi` and can be tuned without
any other code changes.

### Session idle-timeout enforcement scope

**Assumption**: the Redis-backed idle-timeout check (`SessionValidationMiddleware`) is applied to
every endpoint whose ASP.NET Core routing metadata requires authorization (i.e. every
`[Authorize]`-marked endpoint), and is explicitly **not** applied to anonymous endpoints, even if
a client happens to attach a stale/expired `Authorization` header to a public endpoint like
`/auth/login` or `/auth/register` — such a header must never cause a public endpoint to fail.
This is the only correct interpretation that keeps public endpoints genuinely public regardless of
what a client sends.

### Password reset / email confirmation token transport encoding

The specification's example URLs use `token={encodedToken}` without specifying the exact encoding.
**Assumption**: tokens are percent-encoded (`Uri.EscapeDataString`) when embedded into the email
link, matching standard URL-encoding practice for query string values that may contain `+`, `/`,
`=` characters (Identity's tokens are base64-like).

### `GroupKnockout` supported group counts

The specification explicitly lists `{2, 4, 8, 16}` groups as supported — this is implemented as a
hard validation rule (`TournamentGroupCompositionException` for any other count), not merely a
recommendation, since allowing other counts (e.g. 3 groups) would make the "winner faces a
runner-up from a different group, no same-group pairing in round 1" guarantee impossible to
satisfy with a clean power-of-two bracket.

## Technology choices made without an explicit specification value

- **Rate limiter implementation**: ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting`
  (fixed-window) rather than a third-party package, since .NET 8 ships this natively and no
  external dependency is required.
- **OpenTelemetry exporter**: OTLP exporter, enabled only when `OpenTelemetry:OtlpEndpoint` is
  configured; when unset, traces are collected in-process but not exported anywhere (avoids a
  hard dependency on a collector being present in every environment, including CI).
- **Docker base images**: official Microsoft `mcr.microsoft.com/dotnet/sdk:8.0` /
  `mcr.microsoft.com/dotnet/aspnet:8.0`, `mcr.microsoft.com/mssql/server:2022-latest`,
  `redis:7-alpine`, and `axllent/mailpit:latest` (the actively maintained Mailpit image).
