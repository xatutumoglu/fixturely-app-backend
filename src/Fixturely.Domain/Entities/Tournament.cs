using Fixturely.Domain.Common;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;

namespace Fixturely.Domain.Entities;

public sealed class Tournament : SoftDeletableEntity
{
    private readonly List<Participant> _participants = new();
    private readonly List<TournamentMember> _members = new();
    private readonly List<TournamentGroup> _groups = new();
    private readonly List<TournamentRound> _rounds = new();
    private readonly List<Match> _matches = new();
    private readonly List<FixtureGenerationHistory> _fixtureGenerationHistories = new();

    private Tournament()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public Guid OwnerUserId { get; private set; }

    public TournamentFormat Format { get; private set; }

    public LegMode LegMode { get; private set; }

    public TournamentStatus Status { get; private set; }

    public int? NumberOfGroups { get; private set; }

    public bool HasThirdPlaceMatch { get; private set; }

    public int CurrentFixtureGenerationNumber { get; private set; }

    public IReadOnlyCollection<Participant> Participants => _participants.AsReadOnly();

    public IReadOnlyCollection<TournamentMember> Members => _members.AsReadOnly();

    public IReadOnlyCollection<TournamentGroup> Groups => _groups.AsReadOnly();

    public IReadOnlyCollection<TournamentRound> Rounds => _rounds.AsReadOnly();

    public IReadOnlyCollection<Match> Matches => _matches.AsReadOnly();

    public IReadOnlyCollection<FixtureGenerationHistory> FixtureGenerationHistories =>
        _fixtureGenerationHistories.AsReadOnly();

    public static Tournament Create(
        string name,
        string? description,
        Guid ownerUserId,
        TournamentFormat format,
        LegMode legMode,
        int? numberOfGroups,
        bool hasThirdPlaceMatch,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidTournamentStateException(
                ErrorCodes.TournamentNameRequired, "Tournament name is required.");
        }

        if (format is TournamentFormat.GroupStage or TournamentFormat.GroupKnockout)
        {
            if (numberOfGroups is null || numberOfGroups <= 0)
            {
                throw new InvalidTournamentStateException(
                    ErrorCodes.NumberOfGroupsRequired,
                    "Number of groups must be provided for group-based tournament formats.");
            }
        }

        var tournament = new Tournament
        {
            Name = name.Trim(),
            Description = description?.Trim(),
            OwnerUserId = ownerUserId,
            Format = format,
            LegMode = legMode,
            NumberOfGroups = numberOfGroups,
            HasThirdPlaceMatch = hasThirdPlaceMatch,
            Status = TournamentStatus.Draft,
            CurrentFixtureGenerationNumber = 0
        };

        tournament.Initialize(utcNow);
        tournament._members.Add(TournamentMember.CreateOwner(tournament.Id, ownerUserId, utcNow));
        return tournament;
    }

    public void UpdateSettings(
        string name,
        string? description,
        LegMode legMode,
        bool hasThirdPlaceMatch,
        DateTime utcNow)
    {
        EnsureMutable();

        Name = string.IsNullOrWhiteSpace(name) ? Name : name.Trim();
        Description = description?.Trim();
        LegMode = legMode;
        HasThirdPlaceMatch = hasThirdPlaceMatch;
        Touch(utcNow);
    }

    public void MoveToSetup(DateTime utcNow)
    {
        if (Status != TournamentStatus.Draft)
        {
            throw new InvalidTournamentStateException(
                ErrorCodes.OnlyDraftCanMoveToSetup, "Only draft tournaments can move to setup.");
        }

        Status = TournamentStatus.Setup;
        Touch(utcNow);
    }

    public void AddParticipant(Participant participant)
    {
        EnsureMutable();

        if (_participants.Any(p => string.Equals(p.Name, participant.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ParticipantAlreadyExistsException(participant.Name);
        }

        _participants.Add(participant);
    }

    public void RemoveParticipant(Guid participantId, DateTime utcNow)
    {
        EnsureMutable();

        var participant = _participants.FirstOrDefault(p => p.Id == participantId)
            ?? throw new InvalidTournamentStateException(
                ErrorCodes.ParticipantNotFound, "Participant not found in this tournament.");

        _participants.Remove(participant);
        Touch(utcNow);
    }

    public void MarkFixtureGenerated(int generationNumber, DateTime utcNow)
    {
        Status = TournamentStatus.FixtureGenerated;
        CurrentFixtureGenerationNumber = generationNumber;
        Touch(utcNow);
    }

    public void MarkInProgress(DateTime utcNow)
    {
        if (Status is TournamentStatus.FixtureGenerated or TournamentStatus.InProgress)
        {
            Status = TournamentStatus.InProgress;
            Touch(utcNow);
        }
    }

    public void MarkCompleted(DateTime utcNow)
    {
        Status = TournamentStatus.Completed;
        Touch(utcNow);
    }

    public void Reopen(DateTime utcNow)
    {
        if (Status != TournamentStatus.Completed)
        {
            throw new InvalidTournamentStateException(
                ErrorCodes.OnlyCompletedCanReopen, "Only completed tournaments can be reopened.");
        }

        Status = TournamentStatus.InProgress;
        Touch(utcNow);
    }

    public void Archive(DateTime utcNow)
    {
        Status = TournamentStatus.Archived;
        Touch(utcNow);
    }

    public override void MarkAsDeleted(DateTime utcNow)
    {
        Status = TournamentStatus.Deleted;
        base.MarkAsDeleted(utcNow);
    }

    public bool HasAnyScoreEntered()
    {
        return _matches.Any(m => m.Status == MatchStatus.Completed && !m.IsBye);
    }

    public bool CanRegenerateFixture()
    {
        return Status is TournamentStatus.Setup or TournamentStatus.FixtureGenerated
            && !HasAnyScoreEntered();
    }

    private void EnsureMutable()
    {
        if (Status is TournamentStatus.Archived or TournamentStatus.Deleted)
        {
            throw new InvalidTournamentStateException(
                ErrorCodes.TournamentReadOnly, "This tournament is read-only and cannot be modified.");
        }
    }
}
