namespace Fixturely.Domain.Exceptions;

/// <summary>
/// Stable, language-agnostic identifiers for every domain/validation failure the API can
/// return. The backend's own <see cref="Exception.Message"/> strings stay in English (useful
/// for logs and developer tooling), but every response also carries one of these codes via
/// <c>ProblemDetails.errorCode</c> (see Fixturely.Api's ExceptionHandlingMiddleware) so the
/// frontend can render a fully localized message for the user's active UI language instead of
/// showing the backend's raw English text.
/// </summary>
public static class ErrorCodes
{
    public const string TournamentNotFound = "TOURNAMENT_NOT_FOUND";
    public const string UnauthorizedTournamentAccess = "UNAUTHORIZED_TOURNAMENT_ACCESS";
    public const string TournamentNameRequired = "TOURNAMENT_NAME_REQUIRED";
    public const string NumberOfGroupsRequired = "NUMBER_OF_GROUPS_REQUIRED";
    public const string OnlyDraftCanMoveToSetup = "ONLY_DRAFT_CAN_MOVE_TO_SETUP";
    public const string ParticipantAlreadyExists = "PARTICIPANT_ALREADY_EXISTS";
    public const string ParticipantNotFound = "PARTICIPANT_NOT_FOUND";
    public const string OnlyCompletedCanReopen = "ONLY_COMPLETED_CAN_REOPEN";
    public const string TournamentReadOnly = "TOURNAMENT_READ_ONLY";
    public const string TournamentMemberNotFound = "TOURNAMENT_MEMBER_NOT_FOUND";
    public const string OwnerRoleCannotChange = "OWNER_ROLE_CANNOT_CHANGE";
    public const string OwnerCannotBeRemoved = "OWNER_CANNOT_BE_REMOVED";
    public const string MatchNotFound = "MATCH_NOT_FOUND";
    public const string FixtureAlreadyHasScores = "FIXTURE_ALREADY_HAS_SCORES";
    public const string FixtureOnlySetupStatus = "FIXTURE_ONLY_SETUP_STATUS";
    public const string NoPendingFixtureGeneration = "NO_PENDING_FIXTURE_GENERATION";
    public const string NoEngineForFormat = "NO_ENGINE_FOR_FORMAT";
    public const string KnockoutMinParticipants = "KNOCKOUT_MIN_PARTICIPANTS";
    public const string LeagueMinParticipants = "LEAGUE_MIN_PARTICIPANTS";
    public const string GroupStageParticipantCountMismatch = "GROUP_STAGE_PARTICIPANT_COUNT_MISMATCH";
    public const string GroupKnockoutInvalidGroupCount = "GROUP_KNOCKOUT_INVALID_GROUP_COUNT";
    public const string ParticipantCapacityExceeded = "PARTICIPANT_CAPACITY_EXCEEDED";
    public const string PenaltyScoresCannotBeEqual = "PENALTY_SCORES_CANNOT_BE_EQUAL";
    public const string KnockoutMatchNoWinner = "KNOCKOUT_MATCH_NO_WINNER";
    public const string ScoresMustBeNonNegative = "SCORES_MUST_BE_NON_NEGATIVE";
    public const string ByeMatchNoScore = "BYE_MATCH_NO_SCORE";
    public const string ScoreChangeInvalidatesDependents = "SCORE_CHANGE_INVALIDATES_DEPENDENTS";
    public const string InvitationOnlyPendingCanResend = "INVITATION_ONLY_PENDING_CAN_RESEND";
    public const string InvitationOnlyPendingCanRevoke = "INVITATION_ONLY_PENDING_CAN_REVOKE";
    public const string InvitationNoLongerValid = "INVITATION_NO_LONGER_VALID";
    public const string InvitationExpired = "INVITATION_EXPIRED";
    public const string InvitationOnlyOneOwner = "INVITATION_ONLY_ONE_OWNER";
    public const string InvitationOwnerCannotBeInvited = "INVITATION_OWNER_CANNOT_BE_INVITED";
    public const string InvitationAlreadyActive = "INVITATION_ALREADY_ACTIVE";
    public const string InvitationNotFound = "INVITATION_NOT_FOUND";
    public const string InvitationNotFoundOrUsed = "INVITATION_NOT_FOUND_OR_USED";
    public const string UserNotFound = "USER_NOT_FOUND";
    public const string InvitationEmailMismatch = "INVITATION_EMAIL_MISMATCH";
    public const string UserNotRegistered = "USER_NOT_REGISTERED";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string InvalidCredentials = "INVALID_CREDENTIALS";
    public const string EmailNotConfirmed = "EMAIL_NOT_CONFIRMED";
    public const string AccountDisabled = "ACCOUNT_DISABLED";
    public const string InvalidRefreshToken = "INVALID_REFRESH_TOKEN";
    public const string IdentityValidationFailed = "IDENTITY_VALIDATION_FAILED";
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string UnexpectedError = "UNEXPECTED_ERROR";
    public const string InvalidConfirmationRequest = "INVALID_CONFIRMATION_REQUEST";
    public const string InvalidPasswordResetRequest = "INVALID_PASSWORD_RESET_REQUEST";
    public const string DuplicateEmailInBatch = "DUPLICATE_EMAIL_IN_BATCH";

    // FluentValidation field-level codes (shared, language-agnostic identifiers set via
    // `.WithErrorCode(...)` on every rule in Fixturely.Application.Validators.*).
    public const string TournamentNameTooLong = "TOURNAMENT_NAME_TOO_LONG";
    public const string TournamentDescriptionTooLong = "TOURNAMENT_DESCRIPTION_TOO_LONG";
    public const string TournamentFormatInvalid = "TOURNAMENT_FORMAT_INVALID";
    public const string TournamentLegModeInvalid = "TOURNAMENT_LEG_MODE_INVALID";
    public const string RowVersionRequired = "ROW_VERSION_REQUIRED";
    public const string ParticipantNameRequired = "PARTICIPANT_NAME_REQUIRED";
    public const string ParticipantNameTooLong = "PARTICIPANT_NAME_TOO_LONG";
    public const string ParticipantShortCodeTooLong = "PARTICIPANT_SHORT_CODE_TOO_LONG";
    public const string ParticipantsRequired = "PARTICIPANTS_REQUIRED";
    public const string ParticipantsMaxExceeded = "PARTICIPANTS_MAX_EXCEEDED";
    public const string ParticipantIdsRequired = "PARTICIPANT_IDS_REQUIRED";
    public const string TournamentIdsRequired = "TOURNAMENT_IDS_REQUIRED";
    public const string EmailRequired = "EMAIL_REQUIRED";
    public const string EmailInvalid = "EMAIL_INVALID";
    public const string InviteRoleRestricted = "INVITE_ROLE_RESTRICTED";
    public const string ChangeRoleRestricted = "CHANGE_ROLE_RESTRICTED";
    public const string EmailsRequired = "EMAILS_REQUIRED";
    public const string EmailsMaxExceeded = "EMAILS_MAX_EXCEEDED";
    public const string MemberIdsRequired = "MEMBER_IDS_REQUIRED";
    public const string TokenRequired = "TOKEN_REQUIRED";
    public const string VenueTooLong = "VENUE_TOO_LONG";
    public const string ReasonRequired = "REASON_REQUIRED";
    public const string ReasonTooLong = "REASON_TOO_LONG";
    public const string UsernameRequired = "USERNAME_REQUIRED";
    public const string UsernameLengthInvalid = "USERNAME_LENGTH_INVALID";
    public const string UsernameInvalidCharset = "USERNAME_INVALID_CHARSET";
    public const string PasswordRequired = "PASSWORD_REQUIRED";
    public const string PasswordTooShort = "PASSWORD_TOO_SHORT";
    public const string PasswordRequiresUppercase = "PASSWORD_REQUIRES_UPPERCASE";
    public const string PasswordRequiresLowercase = "PASSWORD_REQUIRES_LOWERCASE";
    public const string PasswordRequiresDigit = "PASSWORD_REQUIRES_DIGIT";
    public const string PasswordRequiresSpecialChar = "PASSWORD_REQUIRES_SPECIAL_CHAR";
    public const string EmailOrUsernameRequired = "EMAIL_OR_USERNAME_REQUIRED";
    public const string UserIdRequired = "USER_ID_REQUIRED";
    public const string RefreshTokenRequired = "REFRESH_TOKEN_REQUIRED";
}
