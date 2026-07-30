namespace Fixturely.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

public sealed class TournamentNotFoundException : DomainException
{
    public TournamentNotFoundException(Guid tournamentId)
        : base($"Tournament with id '{tournamentId}' was not found.")
    {
    }
}

public sealed class UnauthorizedTournamentAccessException : DomainException
{
    public UnauthorizedTournamentAccessException(Guid tournamentId, Guid userId)
        : base($"User '{userId}' is not authorized to access tournament '{tournamentId}'.")
    {
    }
}

public sealed class InvalidTournamentStateException : DomainException
{
    public InvalidTournamentStateException(string message) : base(message)
    {
    }
}

public sealed class ParticipantAlreadyExistsException : DomainException
{
    public ParticipantAlreadyExistsException(string participantName)
        : base($"Participant '{participantName}' already exists in this tournament.")
    {
    }
}

public sealed class InvalidFixtureGenerationException : DomainException
{
    public InvalidFixtureGenerationException(string message) : base(message)
    {
    }
}

public sealed class InvalidScoreException : DomainException
{
    public InvalidScoreException(string message) : base(message)
    {
    }
}

public sealed class InvitationException : DomainException
{
    public InvitationException(string message) : base(message)
    {
    }
}

public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string message) : base(message)
    {
    }
}

public sealed class TournamentGroupCompositionException : DomainException
{
    public TournamentGroupCompositionException(string message) : base(message)
    {
    }
}

public sealed class KnockoutPairingException : DomainException
{
    public KnockoutPairingException(string message) : base(message)
    {
    }
}

public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException() : base("The supplied credentials are invalid.")
    {
    }
}

public sealed class EmailNotConfirmedException : DomainException
{
    public EmailNotConfirmedException() : base("Please confirm your email address before logging in.")
    {
    }
}

public sealed class AccountDisabledException : DomainException
{
    public AccountDisabledException() : base("This account has been disabled.")
    {
    }
}

public sealed class InvalidRefreshTokenException : DomainException
{
    public InvalidRefreshTokenException() : base("The refresh token is invalid, expired, or has already been used.")
    {
    }
}

public sealed class IdentityValidationException : DomainException
{
    public IReadOnlyCollection<string> Errors { get; }

    public IdentityValidationException(IEnumerable<string> errors)
        : base(string.Join("; ", errors))
    {
        Errors = errors.ToArray();
    }
}
