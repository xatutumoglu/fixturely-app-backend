using Fixturely.Domain.Common;

namespace Fixturely.Domain.Entities;

public sealed class TournamentGroup : Entity
{
    private readonly List<GroupParticipant> _groupParticipants = new();

    private TournamentGroup()
    {
    }

    public Guid TournamentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public int OrderIndex { get; private set; }

    public IReadOnlyCollection<GroupParticipant> GroupParticipants => _groupParticipants.AsReadOnly();

    public static TournamentGroup Create(Guid tournamentId, string name, int orderIndex, DateTime utcNow)
    {
        var group = new TournamentGroup
        {
            TournamentId = tournamentId,
            Name = name,
            OrderIndex = orderIndex
        };
        group.Initialize(utcNow);
        return group;
    }

    public void AddParticipant(Guid participantId, DateTime utcNow)
    {
        _groupParticipants.Add(GroupParticipant.Create(Id, participantId, utcNow));
    }
}

public sealed class GroupParticipant : Entity
{
    private GroupParticipant()
    {
    }

    public Guid TournamentGroupId { get; private set; }

    public Guid ParticipantId { get; private set; }

    public static GroupParticipant Create(Guid tournamentGroupId, Guid participantId, DateTime utcNow)
    {
        var groupParticipant = new GroupParticipant
        {
            TournamentGroupId = tournamentGroupId,
            ParticipantId = participantId
        };
        groupParticipant.Initialize(utcNow);
        return groupParticipant;
    }
}
