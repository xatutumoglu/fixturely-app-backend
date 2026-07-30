# Authentication and Session Management

## Identity model

`ApplicationUser` (in `Fixturely.Infrastructure.Identity`) extends `IdentityUser<Guid>` with:

- `IsActive` — disabled users cannot authenticate even with correct credentials
  (`AccountDisabledException` → 401).
- `CreatedAtUtc`, `UpdatedAtUtc`, `LastLoginAtUtc` — audit timestamps, always UTC.

`UserName` and `Email` are unique (enforced by ASP.NET Core Identity's normalized-value unique
indexes). Passwords are hashed exclusively by Identity's `PasswordHasher`; Fixturely code never
touches raw password bytes beyond the initial `CreateAsync`/`CheckPasswordAsync` calls, and
passwords are never logged or emailed.

### Password policy

- Minimum length: 8
- At least one uppercase, one lowercase, one digit, one non-alphanumeric character

Configured in `Fixturely.Infrastructure.DependencyInjection.AddInfrastructure` via
`IdentityOptions.Password`.

## Registration → confirmation → login flow

1. `POST /auth/register {userName, email, password}` creates the user (via `IIdentityService`)
   and sends an email confirmation link:
   `{FIXTURELY_FRONTEND__BASEURL}/auth/confirm-email?userId={userId}&token={encodedToken}`.
2. `POST /auth/confirm-email {userId, token}` confirms the email.
3. `POST /auth/login` is **rejected with 401** (`EmailNotConfirmedException`) until step 2 has
   completed — `RequireConfirmedEmail` is enabled on `SignInOptions`, and `AuthService.LoginAsync`
   double-checks `user.EmailConfirmed` explicitly.

`POST /auth/resend-confirmation` and `POST /auth/forgot-password` are **account-enumeration-safe**:
both always return `202 Accepted` regardless of whether the email exists, and do nothing
observable if the account doesn't exist or (for resend) is already confirmed.

## Tokens

### Access token (JWT)

- Signed with `HmacSha256` using `FIXTURELY_JWT__SIGNINGKEY`.
- Claims: `sub`/`NameIdentifier` (user id), `UniqueName`/`Name` (username), `email`, a custom
  `sid` claim (session id, correlates to the Redis session — see below), and `jti`.
- Default lifetime: **15 minutes** (`FIXTURELY_JWT__ACCESSTOKENMINUTES`).
- Returned in the JSON response body of `/auth/login` and `/auth/refresh` — intended to be kept
  in frontend memory only (never persisted to `localStorage`).

### Refresh token

- A cryptographically random 64-byte, URL-safe base64 string.
- **Only its SHA-256 hash is stored** in the `RefreshTokens` table (`TokenHash` column) — the raw
  value is never persisted server-side.
- Delivered exclusively via a `fixturely_refresh_token` cookie: `HttpOnly`, `SameSite=Strict`,
  `Secure` (whenever the request host isn't `localhost`), `Expires` matching the token's own
  expiry (`FIXTURELY_JWT__REFRESHTOKENDAYS`, default 7 days).
- **Rotation**: every call to `POST /auth/refresh` issues a brand-new refresh token and marks the
  presented one as used (`RefreshToken.MarkUsed`) — a used or expired or revoked token cannot be
  reused (`InvalidRefreshTokenException` → 401). This is enforced in `AuthService.RefreshAsync`.
- **Revocation on password reset**: `ResetPasswordAsync` calls `LogoutAllAsync`, which revokes
  every refresh token and ends every `UserSession` row for that user, and clears every Redis
  session key.

## Redis-backed sessions and the idle timeout

Every successful login creates a Redis entry at `fixturely:session:{sessionId}` (via
`RedisSessionStore`, implementing `ISessionStore`) containing the user id, IP, user agent, and
`CreatedAtUtc`/`LastActivityAtUtc`, with a TTL equal to the configured idle timeout
(`FIXTURELY_SESSION__IDLETIMEOUTMINUTES`, default 15 minutes). A companion
`fixturely:user-sessions:{userId}` Redis *set* tracks every session id for a user, enabling
efficient logout-all.

`SessionValidationMiddleware` runs after JWT authentication (`app.UseAuthentication()`) and before
authorization (`app.UseAuthorization()`). For every request whose matched endpoint requires
authorization (i.e. **not** anonymous endpoints like `/auth/login` or `/auth/register`, even if a
stale Bearer token happens to be attached to the request):

1. Extracts the `sid` claim from the validated JWT.
2. Calls `ISessionStore.TouchSessionAsync`, which re-reads the Redis key, and if it still exists,
   re-writes it with a refreshed TTL (sliding expiration) and updated `LastActivityAtUtc`.
3. If the key is missing (idle timeout elapsed, or the session was explicitly ended by
   logout/logout-all/password-reset), the middleware short-circuits with `401 Unauthorized` —
   **even though the JWT signature and expiry are still valid**. This is what makes the idle
   timeout authoritative rather than merely a hint.

Because this check is independent of the JWT's own expiry, a user can be logged out effectively
immediately (logout, logout-all, password reset) without waiting for the access token's 15-minute
natural expiry.

## Tournament-scoped authorization (not global ASP.NET Identity roles)

`TournamentMemberRole` (`Owner`, `ScoreManager`, `Viewer`) is a **per-tournament** value stored on
`TournamentMember`, entirely separate from any ASP.NET Identity role system. Every
tournament-scoped endpoint resolves the caller's role for *that specific tournament id* via
`ITournamentAuthorizationService`:

- `EnsureCanViewAsync` — any active member (Owner, ScoreManager, or Viewer).
- `EnsureCanManageScoresAsync` — Owner or ScoreManager only.
- `EnsureIsOwnerAsync` — Owner only.

These checks always query `TournamentMembers` scoped by both `tournamentId` **and** `userId` —
they never filter a list query by owner id alone and assume the result is authorized, which
prevents IDOR-style access to another user's tournament by guessing a `tournamentId`. If no
active membership row exists, `UnauthorizedTournamentAccessException` is thrown, mapped to
`403 Forbidden`.

## Cookies, CORS, and headers

- CORS is configured with **one explicit origin** (`FIXTURELY_FRONTEND__BASEURL`) and
  `AllowCredentials()` (required for the refresh-token cookie to be sent cross-origin); no
  wildcard origin is used in any environment.
- Refresh/logout endpoints rely on the cookie's `SameSite=Strict` attribute as the primary
  cross-site request forgery mitigation for cookie-carried requests; state-changing requests also
  require a valid Bearer token in the `Authorization` header for authenticated actions, which
  browsers cannot attach cross-origin without explicit script cooperation.
- `SecurityHeadersMiddleware` adds `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`,
  `Referrer-Policy: no-referrer` on every response, and `Strict-Transport-Security` outside
  `Development`. `app.UseHsts()` and `app.UseHttpsRedirection()` are enabled outside Development.

## Rate limiting

`auth-sensitive` (register, login, confirm-email, resend-confirmation, forgot-password,
reset-password) and `invitation-sensitive` (invite, resend invitation, invitation lookup/accept)
named rate-limiter policies are applied per-client-IP with a fixed 1-minute window. In the
`Testing` ASP.NET Core environment these limits are effectively disabled so integration tests
(which legitimately perform many auth calls in quick succession) are not rate-limited.
