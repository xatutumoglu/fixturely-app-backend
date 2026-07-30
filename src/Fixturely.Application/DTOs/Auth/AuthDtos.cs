namespace Fixturely.Application.DTOs.Auth;

public sealed record RegisterRequest(string UserName, string Email, string Password);

public sealed record ConfirmEmailRequest(Guid UserId, string Token);

public sealed record ResendConfirmationRequest(string Email);

public sealed record LoginRequest(string EmailOrUserName, string Password);

public sealed record LoginResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    UserProfileResponse User);

public sealed record RefreshRequest(string RefreshToken);

public sealed record RefreshResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string NewPassword);

public sealed record UserProfileResponse(
    Guid Id,
    string UserName,
    string Email,
    bool EmailConfirmed,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc);

public sealed record AuthTokensResult(
    string AccessToken,
    DateTime AccessTokenExpiresAtUtc,
    string RawRefreshToken,
    DateTime RefreshTokenExpiresAtUtc,
    UserProfileResponse User);
