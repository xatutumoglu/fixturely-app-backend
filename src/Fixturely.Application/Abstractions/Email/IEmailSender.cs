using Fixturely.Domain.Enums;

namespace Fixturely.Application.Abstractions.Email;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default);
}

public sealed class EmailMessage
{
    public required string ToEmail { get; init; }

    public required string Subject { get; init; }

    public required string HtmlBody { get; init; }

    public required string PlainTextBody { get; init; }

    public required EmailTemplateType TemplateType { get; init; }
}

public interface IEmailNotificationService
{
    Task SendEmailConfirmationAsync(
        Guid userId,
        string toEmail,
        string userName,
        string encodedToken,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetAsync(
        Guid userId,
        string toEmail,
        string userName,
        string encodedToken,
        CancellationToken cancellationToken = default);

    Task SendTournamentInvitationAsync(
        string toEmail,
        string tournamentName,
        string invitedByUserName,
        string role,
        string invitationToken,
        CancellationToken cancellationToken = default);

    Task SendInvitationAcceptedAsync(
        string ownerEmail,
        string tournamentName,
        string acceptedByUserName,
        CancellationToken cancellationToken = default);
}
