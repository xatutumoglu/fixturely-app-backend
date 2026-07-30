using Fixturely.Application.Abstractions.Email;
using Fixturely.Infrastructure.Options;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Fixturely.Infrastructure.Email;

/// <summary>
/// Standards-compliant SMTP email sender implemented with MailKit. It talks plain SMTP and
/// therefore works unchanged against Mailpit locally and Brevo (or any other SMTP relay) in
/// production - only the FIXTURELY_SMTP__* configuration values change between environments.
/// </summary>
public sealed class BrevoSmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<BrevoSmtpEmailSender> _logger;

    public BrevoSmtpEmailSender(IOptions<SmtpOptions> options, ILogger<BrevoSmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        var mimeMessage = new MimeMessage();
        mimeMessage.From.Add(new MailboxAddress(_options.FromName, _options.FromEmail));
        mimeMessage.To.Add(MailboxAddress.Parse(message.ToEmail));
        mimeMessage.Subject = message.Subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = message.HtmlBody,
            TextBody = message.PlainTextBody
        };
        mimeMessage.Body = bodyBuilder.ToMessageBody();

        using var client = new SmtpClient();

        try
        {
            var socketOptions = _options.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(_options.Host, _options.Port, socketOptions, cancellationToken);

            if (!string.IsNullOrEmpty(_options.Username))
            {
                await client.AuthenticateAsync(_options.Username, _options.Password ?? string.Empty, cancellationToken);
            }

            await client.SendAsync(mimeMessage, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deliver email of type {TemplateType} to a recipient.", message.TemplateType);
            throw;
        }
    }
}
