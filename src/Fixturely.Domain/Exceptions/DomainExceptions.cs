namespace Fixturely.Domain.Exceptions;

public abstract class DomainException : Exception
{
    /// <summary>
    /// A stable, language-agnostic identifier (see <see cref="ErrorCodes"/>) that lets the
    /// frontend render a fully localized message for the user's active UI language instead of
    /// this exception's own English <see cref="Exception.Message"/>, which exists purely for
    /// logs/developer tooling.
    /// </summary>
    public string ErrorCode { get; }

    /// <summary>
    /// Dynamic values (ids, counts, names) that the frontend interpolates into the localized
    /// message template identified by <see cref="ErrorCode"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Params { get; }

    protected DomainException(string errorCode, string message, IReadOnlyDictionary<string, object?>? errorParams = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Params = errorParams ?? new Dictionary<string, object?>();
    }
}

public sealed class TournamentNotFoundException : DomainException
{
    public TournamentNotFoundException(Guid tournamentId)
        : base(
            ErrorCodes.TournamentNotFound,
            $"Tournament with id '{tournamentId}' was not found.",
            new Dictionary<string, object?> { ["tournamentId"] = tournamentId })
    {
    }
}

public sealed class UnauthorizedTournamentAccessException : DomainException
{
    public UnauthorizedTournamentAccessException(Guid tournamentId, Guid userId)
        : base(
            ErrorCodes.UnauthorizedTournamentAccess,
            $"User '{userId}' is not authorized to access tournament '{tournamentId}'.",
            new Dictionary<string, object?> { ["tournamentId"] = tournamentId, ["userId"] = userId })
    {
    }
}

public sealed class InvalidTournamentStateException : DomainException
{
    public InvalidTournamentStateException(string errorCode, string message, IReadOnlyDictionary<string, object?>? errorParams = null)
        : base(errorCode, message, errorParams)
    {
    }
}

public sealed class ParticipantAlreadyExistsException : DomainException
{
    public ParticipantAlreadyExistsException(string participantName)
        : base(
            ErrorCodes.ParticipantAlreadyExists,
            $"Participant '{participantName}' already exists in this tournament.",
            new Dictionary<string, object?> { ["participantName"] = participantName })
    {
    }
}

public sealed class InvalidFixtureGenerationException : DomainException
{
    public InvalidFixtureGenerationException(string errorCode, string message, IReadOnlyDictionary<string, object?>? errorParams = null)
        : base(errorCode, message, errorParams)
    {
    }
}

public sealed class InvalidScoreException : DomainException
{
    public InvalidScoreException(string errorCode, string message, IReadOnlyDictionary<string, object?>? errorParams = null)
        : base(errorCode, message, errorParams)
    {
    }
}

public sealed class InvitationException : DomainException
{
    public InvitationException(string errorCode, string message, IReadOnlyDictionary<string, object?>? errorParams = null)
        : base(errorCode, message, errorParams)
    {
    }
}

public sealed class UserNotRegisteredException : DomainException
{
    public UserNotRegisteredException(string email)
        : base(
            ErrorCodes.UserNotRegistered,
            $"No Fixturely account was found for '{email}'. Ask them to register first, then try inviting again.",
            new Dictionary<string, object?> { ["email"] = email })
    {
    }
}

public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string message)
        : base(ErrorCodes.ConcurrencyConflict, message)
    {
    }
}

public sealed class TournamentGroupCompositionException : DomainException
{
    public TournamentGroupCompositionException(string errorCode, string message, IReadOnlyDictionary<string, object?>? errorParams = null)
        : base(errorCode, message, errorParams)
    {
    }
}

public sealed class KnockoutPairingException : DomainException
{
    public KnockoutPairingException(string errorCode, string message, IReadOnlyDictionary<string, object?>? errorParams = null)
        : base(errorCode, message, errorParams)
    {
    }
}

public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException()
        : base(ErrorCodes.InvalidCredentials, "The supplied credentials are invalid.")
    {
    }
}

public sealed class EmailNotConfirmedException : DomainException
{
    public EmailNotConfirmedException()
        : base(ErrorCodes.EmailNotConfirmed, "Please confirm your email address before logging in.")
    {
    }
}

public sealed class AccountDisabledException : DomainException
{
    public AccountDisabledException()
        : base(ErrorCodes.AccountDisabled, "This account has been disabled.")
    {
    }
}

public sealed class InvalidRefreshTokenException : DomainException
{
    public InvalidRefreshTokenException()
        : base(ErrorCodes.InvalidRefreshToken, "The refresh token is invalid, expired, or has already been used.")
    {
    }
}

public sealed class IdentityValidationException : DomainException
{
    public IReadOnlyCollection<string> Errors { get; }

    /// <summary>
    /// The stable ASP.NET Core Identity error codes (e.g. "DuplicateEmail",
    /// "PasswordTooShort") backing each entry in <see cref="Errors"/>, in the same order, so the
    /// frontend can render a localized message per failure instead of the English
    /// <see cref="IdentityError.Description"/> text.
    /// </summary>
    public IReadOnlyCollection<string> Codes { get; }

    public IdentityValidationException(IEnumerable<string> errors)
        : this(errors, Array.Empty<string>())
    {
    }

    public IdentityValidationException(IEnumerable<string> errors, IEnumerable<string> codes)
        : base(ErrorCodes.IdentityValidationFailed, string.Join("; ", errors))
    {
        Errors = errors.ToArray();
        Codes = codes.ToArray();
    }
}
