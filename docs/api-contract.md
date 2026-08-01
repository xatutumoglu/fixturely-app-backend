# API Contract

All endpoints are versioned under `/api/v1/...`. Responses use standard HTTP status codes;
errors use RFC 7807 `ProblemDetails` (`application/problem+json`), including a `type`, `title`,
`status`, `detail`, `instance`, and — for validation failures — an `errors` extension keyed by
field name.

Authenticated endpoints require an `Authorization: Bearer <accessToken>` header. The refresh
token is transported exclusively via a `fixturely_refresh_token` `HttpOnly` cookie set by
`/auth/login` and `/auth/refresh`, never in a JSON response body.

## Authentication (`/api/v1/auth`)

| Method | Path                     | Auth | Rate-limited | Description |
|--------|---------------------------|------|---------------|--------------|
| POST   | `/auth/register`          | none | yes | Create a new account; sends an email confirmation link. |
| POST   | `/auth/confirm-email`     | none | yes | Confirm email using `{userId, token}` from the confirmation link. |
| POST   | `/auth/resend-confirmation` | none | yes | Resend confirmation email (account-enumeration-safe: always 202). |
| POST   | `/auth/login`             | none | yes | `{emailOrUserName, password}` → access token + `User` (refresh token set as cookie). |
| POST   | `/auth/refresh`           | none | no  | Rotates refresh token (read from cookie) → new access + refresh token. |
| POST   | `/auth/logout`            | JWT  | no  | Revokes current refresh token + Redis session. |
| POST   | `/auth/logout-all`        | JWT  | no  | Revokes all refresh tokens + all Redis sessions for the user. |
| POST   | `/auth/forgot-password`   | none | yes | Account-enumeration-safe: always 202. |
| POST   | `/auth/reset-password`    | none | yes | `{userId, token, newPassword}`; revokes all sessions/refresh tokens. |
| GET    | `/auth/me`                | JWT  | no  | Current user profile. |

## Tournaments (`/api/v1/tournaments`)

| Method | Path | Auth / Role | Description |
|--------|------|--------------|--------------|
| GET    | `/tournaments` | JWT (member) | Paginated list of tournaments the user owns or is a member of. |
| POST   | `/tournaments` | JWT | Create tournament (caller becomes Owner). |
| GET    | `/tournaments/{tournamentId}` | Viewer+ | Tournament detail, includes `RowVersion`, caller's role, and `MaxParticipants` (see below). |
| PUT    | `/tournaments/{tournamentId}` | Owner | Update settings; requires `RowVersion` (409 on stale write). |
| DELETE | `/tournaments/{tournamentId}` | Owner | Soft-delete. |
| POST   | `/tournaments/{tournamentId}/archive` | Owner | Make read-only. |
| POST   | `/tournaments/{tournamentId}/reopen` | Owner | Reopen a completed tournament. |
| POST   | `/tournaments/{tournamentId}/generate-fixture` | Owner | Generate fixture (Setup status only). |
| POST   | `/tournaments/{tournamentId}/regenerate-fixture` | Owner | Regenerate (only before any score is entered). |
| POST   | `/tournaments/{tournamentId}/confirm-fixture` | Owner | Confirm the current fixture generation. |
| GET    | `/tournaments/{tournamentId}/standings` | Viewer+ | League/group standings with tie-break notes. |
| GET    | `/tournaments/{tournamentId}/groups` | Viewer+ | Group draw with participant assignments. |
| GET    | `/tournaments/{tournamentId}/bracket` | Viewer+ | Knockout bracket nodes with next-match links. |
| GET    | `/tournaments/{tournamentId}/rounds` | Viewer+ | All rounds (league/group/knockout/final/third-place). |
| GET    | `/tournaments/{tournamentId}/audit-logs` | Viewer+ | Recent audit log entries (most recent 200). |

`MaxParticipants` (nullable `int`) on the tournament detail response is `NumberOfGroups * 4` for
**Group stage** and **Group + Knockout** formats (their fixture engine requires groups of exactly
four), and `null` for **League**/**Knockout**, which accept any participant count ≥ 2. The
participant-add and bulk-add endpoints below enforce this same limit server-side
(`400 TournamentGroupCompositionException` if exceeded) — clients should treat `MaxParticipants`
as informational only, never as the sole guard.

## Participants (`/api/v1/tournaments/{tournamentId}/participants`)

| Method | Path | Auth / Role | Description |
|--------|------|--------------|--------------|
| GET    | `/participants` | Viewer+ | List active participants. |
| POST   | `/participants` | Owner | Add one participant (unique name per tournament); rejected once `MaxParticipants` would be exceeded. |
| POST   | `/participants/bulk` | Owner | Add up to 200 participants in a single call (`{ participants: [{name, shortCode}, ...] }`); all-or-nothing — a name collision or capacity overflow fails the entire batch, no partial insert. |
| PUT    | `/participants/{participantId}` | Owner | Rename / update short code. |
| DELETE | `/participants/{participantId}` | Owner | Soft-delete. |
| DELETE | `/participants/bulk` | Owner | Soft-delete many at once (`{ participantIds: [...] }`); unknown ids are silently skipped, never an error. |

## Members and invitations

| Method | Path | Auth / Role | Description |
|--------|------|--------------|--------------|
| GET    | `/tournaments/{tournamentId}/members` | Viewer+ | List active members with role. |
| PUT    | `/tournaments/{tournamentId}/members/{memberId}/role` | Owner | Change role (ScoreManager/Viewer only). |
| DELETE | `/tournaments/{tournamentId}/members/{memberId}` | Owner | Remove member. |
| DELETE | `/tournaments/{tournamentId}/members/bulk` | Owner | Remove many members at once (`{ memberIds: [...] }`); returns a `200` array with one `{memberId, success, error}` result per id — e.g. attempting to remove the Owner fails just that entry, the rest still succeed. |
| POST   | `/tournaments/{tournamentId}/invitations` | Owner (rate-limited) | Invite by email + role. |
| POST   | `/tournaments/{tournamentId}/invitations/bulk` | Owner (rate-limited) | Invite up to 100 emails at once with one shared role (`{ emails: [...], role }`); returns a `200` array with one `{email, success, invitation, error}` result per address — an unregistered or duplicate email fails only its own entry. |
| POST   | `/tournaments/{tournamentId}/invitations/{invitationId}/resend` | Owner (rate-limited) | Resend, rotates token. |
| DELETE | `/tournaments/{tournamentId}/invitations/{invitationId}` | Owner | Revoke pending invitation. |
| GET    | `/invitations/{token}` | none (rate-limited) | Inspect an invitation before accepting. |
| POST   | `/invitations/{token}/accept` | JWT (rate-limited) | Accept; caller's email must match the invited email. |

## Matches (`/api/v1/tournaments/{tournamentId}/matches`)

| Method | Path | Auth / Role | Description |
|--------|------|--------------|--------------|
| GET    | `/matches` | Viewer+ | List all matches for the tournament. |
| GET    | `/matches/{matchId}` | Viewer+ | Single match detail. |
| PUT    | `/matches/{matchId}/score` | Owner or ScoreManager | Enter/correct score; requires `RowVersion`. |
| PUT    | `/matches/{matchId}/schedule` | Owner | Set scheduled date/time and venue. |
| POST   | `/matches/{matchId}/invalidate` | Owner | Explicitly invalidate a match with a reason. |

### Score correction and dependent-match invalidation

`PUT /matches/{matchId}/score` accepts an optional `confirmDependentInvalidation` flag. If
correcting an already-completed knockout leg would change the winner of a tie whose result has
already propagated to later rounds, the endpoint returns `400 Bad Request` with the list of
affected downstream match ids **unless** `confirmDependentInvalidation: true` is supplied, in
which case the downstream matches are invalidated and their participant slots cleared
(see [tournament-rules.md](tournament-rules.md)).

## Swagger

The full machine-readable OpenAPI document is served at `/swagger/v1/swagger.json` and rendered
at `/swagger` (Development environment). It documents every request/response schema, the Bearer
security scheme, and per-endpoint parameter/response shapes.
