using Fixturely.Application.Abstractions.Caching;
using Fixturely.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Fixturely.Api.Middleware;

/// <summary>
/// After JWT authentication succeeds, validates that the session referenced by the token's
/// "sid" claim is still present in Redis (fixturely:session:{sessionId}) and has not expired
/// due to the sliding idle timeout. On success the session's activity timestamp is refreshed.
/// If the session is missing or expired, the request is rejected with 401 even though the
/// JWT signature itself is still valid, which is what enforces the idle-timeout requirement.
///
/// This check only applies to endpoints that actually require authorization. A stale or
/// revoked Bearer token attached to an anonymous endpoint (e.g. login, register) must never
/// cause that public endpoint to be rejected.
/// </summary>
public sealed class SessionValidationMiddleware
{
    private readonly RequestDelegate _next;

    public SessionValidationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ISessionStore sessionStore,
        TimeProvider timeProvider,
        IOptions<Fixturely.Application.Common.SessionOptions> sessionOptions)
    {
        var endpoint = context.GetEndpoint();
        var requiresAuthorization = endpoint?.Metadata.GetMetadata<IAuthorizeData>() is not null;
        var allowsAnonymous = endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null;

        if (requiresAuthorization && !allowsAnonymous && context.User.Identity?.IsAuthenticated == true)
        {
            var sessionId = context.User.FindFirst("sid")?.Value;

            if (string.IsNullOrEmpty(sessionId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var utcNow = timeProvider.GetUtcNow().UtcDateTime;
            var idleTimeout = TimeSpan.FromMinutes(sessionOptions.Value.IdleTimeoutMinutes);
            var touched = await sessionStore.TouchSessionAsync(sessionId, utcNow, idleTimeout, context.RequestAborted);

            if (!touched)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }
        }

        await _next(context);
    }
}
