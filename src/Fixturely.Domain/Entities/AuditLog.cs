using Fixturely.Domain.Common;

namespace Fixturely.Domain.Entities;

public sealed class AuditLog : Entity
{
    private AuditLog()
    {
    }

    public Guid? UserId { get; private set; }

    public Guid? TournamentId { get; private set; }

    public string Category { get; private set; } = string.Empty;

    public string Action { get; private set; } = string.Empty;

    public string? OldValuesJson { get; private set; }

    public string? NewValuesJson { get; private set; }

    public string? Reason { get; private set; }

    public DateTime OccurredAtUtc { get; private set; }

    public static AuditLog Create(
        Guid? userId,
        Guid? tournamentId,
        string category,
        string action,
        string? oldValuesJson,
        string? newValuesJson,
        string? reason,
        DateTime utcNow)
    {
        var log = new AuditLog
        {
            UserId = userId,
            TournamentId = tournamentId,
            Category = category,
            Action = action,
            OldValuesJson = oldValuesJson,
            NewValuesJson = newValuesJson,
            Reason = reason,
            OccurredAtUtc = utcNow
        };
        log.Initialize(utcNow);
        return log;
    }
}
