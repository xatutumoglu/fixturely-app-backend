using Fixturely.Application.Abstractions.Identity;
using Fixturely.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Fixturely.Infrastructure.Identity;

public sealed class IdentityService : IIdentityService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly TimeProvider _timeProvider;

    public IdentityService(UserManager<ApplicationUser> userManager, TimeProvider timeProvider)
    {
        _userManager = userManager;
        _timeProvider = timeProvider;
    }

    public async Task<(IdentityOperationResult Result, Guid? UserId)> CreateUserAsync(
        string userName,
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            IsActive = true,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        var result = await _userManager.CreateAsync(user, password);

        return result.Succeeded
            ? (IdentityOperationResult.Success(), user.Id)
            : (IdentityOperationResult.Failure(
                result.Errors.Select(e => e.Description), result.Errors.Select(e => e.Code)), null);
    }

    public async Task<UserRecord?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByEmailAsync(email);
        return user is null ? null : Map(user);
    }

    public async Task<UserRecord?> FindByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByNameAsync(userName);
        return user is null ? null : Map(user);
    }

    public async Task<UserRecord?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : Map(user);
    }

    public async Task<string> GenerateEmailConfirmationTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        return await _userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    public async Task<IdentityOperationResult> ConfirmEmailAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return IdentityOperationResult.Failure("Invalid confirmation request.", Fixturely.Domain.Exceptions.ErrorCodes.InvalidConfirmationRequest);
        }

        var result = await _userManager.ConfirmEmailAsync(user, token);

        if (result.Succeeded)
        {
            user.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            await _userManager.UpdateAsync(user);
        }

        return result.Succeeded
            ? IdentityOperationResult.Success()
            : IdentityOperationResult.Failure(result.Errors.Select(e => e.Description), result.Errors.Select(e => e.Code));
    }

    public async Task<bool> CheckPasswordAsync(
        Guid userId,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());
        return user is not null && await _userManager.CheckPasswordAsync(user, password);
    }

    public async Task<string> GeneratePasswordResetTokenAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString())
            ?? throw new InvalidOperationException("User not found.");

        return await _userManager.GeneratePasswordResetTokenAsync(user);
    }

    public async Task<IdentityOperationResult> ResetPasswordAsync(
        Guid userId,
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return IdentityOperationResult.Failure("Invalid password reset request.", Fixturely.Domain.Exceptions.ErrorCodes.InvalidPasswordResetRequest);
        }

        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);

        if (result.Succeeded)
        {
            user.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
            await _userManager.UpdateSecurityStampAsync(user);
        }

        return result.Succeeded
            ? IdentityOperationResult.Success()
            : IdentityOperationResult.Failure(result.Errors.Select(e => e.Description), result.Errors.Select(e => e.Code));
    }

    public async Task SetLastLoginAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is null)
        {
            return;
        }

        user.LastLoginAtUtc = utcNow;
        user.UpdatedAtUtc = utcNow;
        await _userManager.UpdateAsync(user);
    }

    public async Task InvalidateSecurityStampAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId.ToString());

        if (user is not null)
        {
            await _userManager.UpdateSecurityStampAsync(user);
        }
    }

    private static UserRecord Map(ApplicationUser user) => new()
    {
        Id = user.Id,
        UserName = user.UserName ?? string.Empty,
        Email = user.Email ?? string.Empty,
        EmailConfirmed = user.EmailConfirmed,
        IsActive = user.IsActive,
        CreatedAtUtc = user.CreatedAtUtc,
        LastLoginAtUtc = user.LastLoginAtUtc
    };
}
