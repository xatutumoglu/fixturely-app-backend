using Fixturely.Domain.Common;

namespace Fixturely.Domain.Entities;

public sealed class RefreshToken : Entity
{
    private RefreshToken()
    {
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = string.Empty;

    public string SessionId { get; private set; } = string.Empty;

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public string? ReplacedByTokenHash { get; private set; }

    public string? CreatedByIp { get; private set; }

    public bool IsUsed { get; private set; }

    public static RefreshToken Create(
        Guid userId,
        string tokenHash,
        string sessionId,
        DateTime expiresAtUtc,
        string? createdByIp,
        DateTime utcNow)
    {
        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            SessionId = sessionId,
            ExpiresAtUtc = expiresAtUtc,
            CreatedByIp = createdByIp
        };
        token.Initialize(utcNow);
        return token;
    }

    public bool IsActive(DateTime utcNow)
    {
        return RevokedAtUtc is null && !IsUsed && utcNow < ExpiresAtUtc;
    }

    public void MarkUsed(string replacedByTokenHash, DateTime utcNow)
    {
        IsUsed = true;
        ReplacedByTokenHash = replacedByTokenHash;
        Touch(utcNow);
    }

    public void Revoke(DateTime utcNow)
    {
        RevokedAtUtc = utcNow;
        Touch(utcNow);
    }
}
