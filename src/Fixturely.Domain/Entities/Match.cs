using Fixturely.Domain.Common;
using Fixturely.Domain.Enums;
using Fixturely.Domain.Exceptions;

namespace Fixturely.Domain.Entities;

public sealed class Match : Entity
{
    private Match()
    {
    }

    public Guid TournamentId { get; private set; }

    public Guid RoundId { get; private set; }

    public Guid? TournamentGroupId { get; private set; }

    public Guid? HomeParticipantId { get; private set; }

    public Guid? AwayParticipantId { get; private set; }

    public MatchStatus Status { get; private set; }

    public DateTime? ScheduledAtUtc { get; private set; }

    public string? Venue { get; private set; }

    public int? HomeRegularTimeScore { get; private set; }

    public int? AwayRegularTimeScore { get; private set; }

    public int? HomeExtraTimeScore { get; private set; }

    public int? AwayExtraTimeScore { get; private set; }

    public int? HomePenaltyScore { get; private set; }

    public int? AwayPenaltyScore { get; private set; }

    public Guid? WinnerParticipantId { get; private set; }

    public int LegNumber { get; private set; }

    public Guid? TieIdentifier { get; private set; }

    public bool IsBye { get; private set; }

    public bool IsThirdPlaceMatch { get; private set; }

    public bool RequiresDecisiveWinner { get; private set; }

    /// <summary>
    /// The match that receives this match's WINNER once it is decided (e.g. semifinal to final).
    /// Null when this match is the final or otherwise has no forward destination for its winner.
    /// </summary>
    public Guid? NextHomeMatchId { get; private set; }

    /// <summary>
    /// The match that receives this match's LOSER once it is decided. Used exclusively for
    /// semifinal matches that feed a third-place match. Null in every other case.
    /// </summary>
    public Guid? NextAwayMatchId { get; private set; }

    /// <summary>Whether the winner is placed in the home slot of <see cref="NextHomeMatchId"/>.</summary>
    public bool NextHomeMatchSlotIsHome { get; private set; }

    /// <summary>Whether the loser is placed in the home slot of <see cref="NextAwayMatchId"/>.</summary>
    public bool NextAwayMatchSlotIsHome { get; private set; }

    public int BracketSlotIndex { get; private set; }

    public int? HomeQualifierGroupOrderIndex { get; private set; }

    public int? HomeQualifierPosition { get; private set; }

    public int? AwayQualifierGroupOrderIndex { get; private set; }

    public int? AwayQualifierPosition { get; private set; }

    public static Match CreateLeagueOrGroupMatch(
        Guid tournamentId,
        Guid roundId,
        Guid? groupId,
        Guid? homeParticipantId,
        Guid? awayParticipantId,
        int legNumber,
        DateTime utcNow)
    {
        var isBye = homeParticipantId is null || awayParticipantId is null;

        var match = new Match
        {
            TournamentId = tournamentId,
            RoundId = roundId,
            TournamentGroupId = groupId,
            HomeParticipantId = homeParticipantId,
            AwayParticipantId = awayParticipantId,
            LegNumber = legNumber,
            IsBye = isBye,
            RequiresDecisiveWinner = false,
            Status = isBye ? MatchStatus.Completed : MatchStatus.Pending
        };

        if (isBye)
        {
            match.WinnerParticipantId = homeParticipantId ?? awayParticipantId;
        }

        match.Initialize(utcNow);
        return match;
    }

    public static Match CreateKnockoutMatch(
        Guid tournamentId,
        Guid roundId,
        Guid? homeParticipantId,
        Guid? awayParticipantId,
        int legNumber,
        Guid tieIdentifier,
        bool isThirdPlaceMatch,
        int bracketSlotIndex,
        bool isBye,
        DateTime utcNow,
        int? homeQualifierGroupOrderIndex = null,
        int? homeQualifierPosition = null,
        int? awayQualifierGroupOrderIndex = null,
        int? awayQualifierPosition = null)
    {
        var match = new Match
        {
            TournamentId = tournamentId,
            RoundId = roundId,
            HomeParticipantId = homeParticipantId,
            AwayParticipantId = awayParticipantId,
            LegNumber = legNumber,
            TieIdentifier = tieIdentifier,
            IsBye = isBye,
            IsThirdPlaceMatch = isThirdPlaceMatch,
            RequiresDecisiveWinner = true,
            BracketSlotIndex = bracketSlotIndex,
            HomeQualifierGroupOrderIndex = homeQualifierGroupOrderIndex,
            HomeQualifierPosition = homeQualifierPosition,
            AwayQualifierGroupOrderIndex = awayQualifierGroupOrderIndex,
            AwayQualifierPosition = awayQualifierPosition,
            Status = isBye ? MatchStatus.Completed : MatchStatus.Pending
        };

        if (isBye)
        {
            match.WinnerParticipantId = homeParticipantId ?? awayParticipantId;
        }

        match.Initialize(utcNow);
        return match;
    }

    public void AssignParticipant(bool isHomeSlot, Guid participantId, DateTime utcNow)
    {
        if (isHomeSlot)
        {
            HomeParticipantId = participantId;
        }
        else
        {
            AwayParticipantId = participantId;
        }

        if (HomeParticipantId is not null && AwayParticipantId is not null && Status == MatchStatus.Pending)
        {
            Status = MatchStatus.Scheduled;
        }

        Touch(utcNow);
    }

    public void ResolveQualifierSlot(bool isHomeSlot, Guid participantId, DateTime utcNow)
    {
        AssignParticipant(isHomeSlot, participantId, utcNow);

        if (isHomeSlot)
        {
            HomeQualifierGroupOrderIndex = null;
            HomeQualifierPosition = null;
        }
        else
        {
            AwayQualifierGroupOrderIndex = null;
            AwayQualifierPosition = null;
        }

        if (HomeParticipantId is not null && AwayParticipantId is not null && Status == MatchStatus.Pending)
        {
            Status = MatchStatus.Scheduled;
        }
    }

    public void LinkNextMatchSlots(
        Guid? nextHomeMatchId,
        bool nextHomeMatchSlotIsHome,
        Guid? nextAwayMatchId,
        bool nextAwayMatchSlotIsHome,
        DateTime utcNow)
    {
        NextHomeMatchId = nextHomeMatchId;
        NextHomeMatchSlotIsHome = nextHomeMatchSlotIsHome;
        NextAwayMatchId = nextAwayMatchId;
        NextAwayMatchSlotIsHome = nextAwayMatchSlotIsHome;
        Touch(utcNow);
    }

    public void Schedule(DateTime? scheduledAtUtc, string? venue, DateTime utcNow)
    {
        ScheduledAtUtc = scheduledAtUtc;
        Venue = venue;
        Touch(utcNow);
    }

    public void SetRegularTimeScore(int homeScore, int awayScore, DateTime utcNow)
    {
        EnsureNonNegative(homeScore, awayScore);

        HomeRegularTimeScore = homeScore;
        AwayRegularTimeScore = awayScore;
        HomeExtraTimeScore = null;
        AwayExtraTimeScore = null;
        HomePenaltyScore = null;
        AwayPenaltyScore = null;
        WinnerParticipantId = null;
        Status = MatchStatus.InProgress;
        Touch(utcNow);
    }

    public void SetExtraTimeScore(int homeScore, int awayScore, DateTime utcNow)
    {
        EnsureNonNegative(homeScore, awayScore);

        HomeExtraTimeScore = homeScore;
        AwayExtraTimeScore = awayScore;
        HomePenaltyScore = null;
        AwayPenaltyScore = null;
        WinnerParticipantId = null;
        Touch(utcNow);
    }

    public void SetPenaltyScore(int homeScore, int awayScore, DateTime utcNow)
    {
        EnsureNonNegative(homeScore, awayScore);

        if (homeScore == awayScore)
        {
            throw new InvalidScoreException(
                ErrorCodes.PenaltyScoresCannotBeEqual, "Penalty shoot-out scores cannot be equal.");
        }

        HomePenaltyScore = homeScore;
        AwayPenaltyScore = awayScore;
        WinnerParticipantId = null;
        Touch(utcNow);
    }

    public void CompleteWithWinner(Guid? winnerParticipantId, DateTime utcNow)
    {
        if (RequiresDecisiveWinner && winnerParticipantId is null && !IsBye)
        {
            throw new InvalidScoreException(
                ErrorCodes.KnockoutMatchNoWinner,
                "A knockout match cannot be completed without a determinable winner.");
        }

        WinnerParticipantId = winnerParticipantId;
        Status = MatchStatus.Completed;
        Touch(utcNow);
    }

    public void Invalidate(DateTime utcNow)
    {
        Status = MatchStatus.Invalidated;
        WinnerParticipantId = null;
        HomeRegularTimeScore = null;
        AwayRegularTimeScore = null;
        HomeExtraTimeScore = null;
        AwayExtraTimeScore = null;
        HomePenaltyScore = null;
        AwayPenaltyScore = null;
        Touch(utcNow);
    }

    public void ClearParticipantSlot(bool isHomeSlot, DateTime utcNow)
    {
        if (isHomeSlot)
        {
            HomeParticipantId = null;
        }
        else
        {
            AwayParticipantId = null;
        }

        Status = MatchStatus.Pending;
        WinnerParticipantId = null;
        HomeRegularTimeScore = null;
        AwayRegularTimeScore = null;
        HomeExtraTimeScore = null;
        AwayExtraTimeScore = null;
        HomePenaltyScore = null;
        AwayPenaltyScore = null;
        Touch(utcNow);
    }

    public bool IsRegularTimeDecisive()
    {
        return HomeRegularTimeScore is not null
            && AwayRegularTimeScore is not null
            && HomeRegularTimeScore != AwayRegularTimeScore;
    }

    public bool IsExtraTimeDecisive()
    {
        return HomeExtraTimeScore is not null
            && AwayExtraTimeScore is not null
            && HomeExtraTimeScore != AwayExtraTimeScore;
    }

    private static void EnsureNonNegative(int homeScore, int awayScore)
    {
        if (homeScore < 0 || awayScore < 0)
        {
            throw new InvalidScoreException(
                ErrorCodes.ScoresMustBeNonNegative, "Scores must be non-negative integers.");
        }
    }
}
