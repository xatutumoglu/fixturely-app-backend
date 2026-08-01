namespace Fixturely.Application.Abstractions.Identity;

public sealed class UserRecord
{
    public required Guid Id { get; init; }

    public required string UserName { get; init; }

    public required string Email { get; init; }

    public required bool EmailConfirmed { get; init; }

    public required bool IsActive { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? LastLoginAtUtc { get; init; }
}

public sealed class IdentityOperationResult
{
    public bool Succeeded { get; private init; }

    public IReadOnlyCollection<string> Errors { get; private init; } = Array.Empty<string>();

    /// <summary>
    /// The stable ASP.NET Core Identity error codes (e.g. "DuplicateEmail", "PasswordTooShort")
    /// backing each entry in <see cref="Errors"/>, in the same order, so callers can render a
    /// localized message per failure instead of the English <c>IdentityError.Description</c>.
    /// </summary>
    public IReadOnlyCollection<string> Codes { get; private init; } = Array.Empty<string>();

    public static IdentityOperationResult Success() => new() { Succeeded = true };

    public static IdentityOperationResult Failure(IEnumerable<string> errors) =>
        new() { Succeeded = false, Errors = errors.ToArray() };

    public static IdentityOperationResult Failure(IEnumerable<string> errors, IEnumerable<string> codes) =>
        new() { Succeeded = false, Errors = errors.ToArray(), Codes = codes.ToArray() };

    public static IdentityOperationResult Failure(string error) => Failure(new[] { error });

    public static IdentityOperationResult Failure(string error, string code) =>
        Failure(new[] { error }, new[] { code });
}

public interface IIdentityService
{
    Task<(IdentityOperationResult Result, Guid? UserId)> CreateUserAsync(
        string userName,
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<UserRecord?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<string> GenerateEmailConfirmationTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> ConfirmEmailAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default);

    Task<bool> CheckPasswordAsync(Guid userId, string password, CancellationToken cancellationToken = default);

    Task<string> GeneratePasswordResetTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IdentityOperationResult> ResetPasswordAsync(
        Guid userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);

    Task SetLastLoginAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);

    Task InvalidateSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default);
}
