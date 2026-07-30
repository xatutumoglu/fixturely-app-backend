using System.Security.Claims;
using Fixturely.Application.Auth;
using Fixturely.Application.DTOs.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fixturely.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private const string RefreshTokenCookieName = "fixturely_refresh_token";

    private readonly AuthService _authService;

    public AuthController(AuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        await _authService.RegisterAsync(request, cancellationToken);
        return Accepted();
    }

    [HttpPost("confirm-email")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken cancellationToken)
    {
        await _authService.ConfirmEmailAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpPost("resend-confirmation")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ResendConfirmationAsync(
        ResendConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        await _authService.ResendConfirmationAsync(request, cancellationToken);
        return Accepted();
    }

    [HttpPost("login")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<ActionResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _authService.LoginAsync(request, ipAddress, userAgent, cancellationToken);

        SetRefreshTokenCookie(result.RawRefreshToken, result.RefreshTokenExpiresAtUtc);

        return Ok(new LoginResponse(result.AccessToken, result.AccessTokenExpiresAtUtc, result.User));
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<RefreshResponse>> RefreshAsync(CancellationToken cancellationToken)
    {
        var rawRefreshToken = Request.Cookies[RefreshTokenCookieName];

        if (string.IsNullOrEmpty(rawRefreshToken))
        {
            return Unauthorized();
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshAsync(rawRefreshToken, ipAddress, cancellationToken);

        SetRefreshTokenCookie(result.RawRefreshToken, result.RefreshTokenExpiresAtUtc);

        return Ok(new RefreshResponse(result.AccessToken, result.AccessTokenExpiresAtUtc));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> LogoutAsync(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var sessionId = User.FindFirstValue("sid");
        var rawRefreshToken = Request.Cookies[RefreshTokenCookieName];

        await _authService.LogoutAsync(userId, sessionId, rawRefreshToken, cancellationToken);

        Response.Cookies.Delete(RefreshTokenCookieName);
        return NoContent();
    }

    [HttpPost("logout-all")]
    [Authorize]
    public async Task<IActionResult> LogoutAllAsync(CancellationToken cancellationToken)
    {
        await _authService.LogoutAllAsync(GetUserId(), cancellationToken);
        Response.Cookies.Delete(RefreshTokenCookieName);
        return NoContent();
    }

    [HttpPost("forgot-password")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ForgotPasswordAsync(request, cancellationToken);
        return Accepted();
    }

    [HttpPost("reset-password")]
    [EnableRateLimiting("auth-sensitive")]
    public async Task<IActionResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _authService.ResetPasswordAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfileResponse>> GetMeAsync(CancellationToken cancellationToken)
    {
        var profile = await _authService.GetProfileAsync(GetUserId(), cancellationToken);
        return Ok(profile);
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private void SetRefreshTokenCookie(string rawRefreshToken, DateTime expiresAtUtc)
    {
        Response.Cookies.Append(RefreshTokenCookieName, rawRefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = !HttpContext.Request.Host.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase),
            SameSite = SameSiteMode.Strict,
            Expires = expiresAtUtc
        });
    }
}
