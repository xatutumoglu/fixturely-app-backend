namespace Fixturely.Domain.Enums;

public enum EmailDeliveryStatus
{
    Queued = 0,
    Sent = 1,
    Failed = 2
}

public enum EmailTemplateType
{
    EmailConfirmation = 0,
    ResendConfirmation = 1,
    PasswordReset = 2,
    TournamentInvitation = 3,
    InvitationAccepted = 4
}
