using Fixturely.Application.Abstractions.Email;
using Fixturely.Application.Abstractions.Persistence;
using Fixturely.Domain.Entities;
using Fixturely.Domain.Enums;
using Fixturely.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fixturely.Infrastructure.Email;

public sealed class EmailNotificationService : IEmailNotificationService
{
    private readonly IEmailSender _emailSender;
    private readonly IApplicationDbContext _dbContext;
    private readonly FrontendOptions _frontendOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IEmailSender emailSender,
        IApplicationDbContext dbContext,
        IOptions<FrontendOptions> frontendOptions,
        TimeProvider timeProvider,
        ILogger<EmailNotificationService> logger)
    {
        _emailSender = emailSender;
        _dbContext = dbContext;
        _frontendOptions = frontendOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task SendEmailConfirmationAsync(
        Guid userId,
        string toEmail,
        string userName,
        string encodedToken,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_frontendOptions.BaseUrl}/auth/confirm-email?userId={userId}&token={Uri.EscapeDataString(encodedToken)}";

        var html = $"<p>Hi {userName},</p>" +
                   "<p>Thanks for registering with Fixturely. Please confirm your email address by clicking the link below:</p>" +
                   $"<p><a href=\"{url}\">Confirm my email</a></p>" +
                   "<p>If you did not create this account, you can ignore this email.</p>";

        var text = $"Hi {userName},\n\nPlease confirm your email address: {url}\n\n" +
                   "If you did not create this account, you can ignore this email.";

        return SendAndRecordAsync(userId, toEmail, "Confirm your Fixturely email address", html, text,
            EmailTemplateType.EmailConfirmation, cancellationToken);
    }

    public Task SendPasswordResetAsync(
        Guid userId,
        string toEmail,
        string userName,
        string encodedToken,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_frontendOptions.BaseUrl}/auth/reset-password?userId={userId}&token={Uri.EscapeDataString(encodedToken)}";

        var html = $"<p>Hi {userName},</p>" +
                   "<p>We received a request to reset your Fixturely password. Click the link below to choose a new password:</p>" +
                   $"<p><a href=\"{url}\">Reset my password</a></p>" +
                   "<p>If you did not request this, you can safely ignore this email.</p>";

        var text = $"Hi {userName},\n\nReset your password: {url}\n\nIf you did not request this, ignore this email.";

        return SendAndRecordAsync(userId, toEmail, "Reset your Fixturely password", html, text,
            EmailTemplateType.PasswordReset, cancellationToken);
    }

    public Task SendTournamentInvitationAsync(
        string toEmail,
        string tournamentName,
        string invitedByUserName,
        string role,
        string invitationToken,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_frontendOptions.BaseUrl}/invitations/accept?token={Uri.EscapeDataString(invitationToken)}";

        var html = $"<p>{invitedByUserName} invited you to join the tournament \"{tournamentName}\" on Fixturely as {role}.</p>" +
                   $"<p><a href=\"{url}\">Accept the invitation</a></p>";

        var text = $"{invitedByUserName} invited you to join \"{tournamentName}\" on Fixturely as {role}.\n\n{url}";

        return SendAndRecordAsync(null, toEmail, $"You're invited to {tournamentName} on Fixturely", html, text,
            EmailTemplateType.TournamentInvitation, cancellationToken);
    }

    public Task SendInvitationAcceptedAsync(
        string ownerEmail,
        string tournamentName,
        string acceptedByUserName,
        CancellationToken cancellationToken = default)
    {
        var html = $"<p>{acceptedByUserName} has accepted your invitation to \"{tournamentName}\".</p>";
        var text = $"{acceptedByUserName} has accepted your invitation to \"{tournamentName}\".";

        return SendAndRecordAsync(null, ownerEmail, $"Invitation accepted for {tournamentName}", html, text,
            EmailTemplateType.InvitationAccepted, cancellationToken);
    }

    private async Task SendAndRecordAsync(
        Guid? userId,
        string toEmail,
        string subject,
        string html,
        string text,
        EmailTemplateType templateType,
        CancellationToken cancellationToken)
    {
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            await _emailSender.SendAsync(
                new EmailMessage
                {
                    ToEmail = toEmail,
                    Subject = subject,
                    HtmlBody = html,
                    PlainTextBody = text,
                    TemplateType = templateType
                },
                cancellationToken);

            _dbContext.EmailDeliveryEvents.Add(EmailDeliveryEvent.CreateSent(userId, toEmail, templateType, utcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email delivery failed for template {TemplateType}.", templateType);
            _dbContext.EmailDeliveryEvents.Add(
                EmailDeliveryEvent.CreateFailed(userId, toEmail, templateType, ex.GetType().Name, utcNow));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
