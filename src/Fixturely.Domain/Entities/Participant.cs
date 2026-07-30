using Fixturely.Domain.Common;

namespace Fixturely.Domain.Entities;

public sealed class Participant : Entity
{
    private Participant()
    {
    }

    public Guid TournamentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? ShortCode { get; private set; }

    public bool IsDeleted { get; private set; }

    public static Participant Create(Guid tournamentId, string name, string? shortCode, DateTime utcNow)
    {
        var participant = new Participant
        {
            TournamentId = tournamentId,
            Name = name.Trim(),
            ShortCode = shortCode?.Trim()
        };
        participant.Initialize(utcNow);
        return participant;
    }

    public void Update(string name, string? shortCode, DateTime utcNow)
    {
        Name = name.Trim();
        ShortCode = shortCode?.Trim();
        Touch(utcNow);
    }

    public void MarkDeleted(DateTime utcNow)
    {
        IsDeleted = true;
        Touch(utcNow);
    }
}
