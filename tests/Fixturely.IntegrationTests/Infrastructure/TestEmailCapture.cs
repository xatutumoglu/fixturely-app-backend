using System.Collections.Concurrent;
using Fixturely.Application.Abstractions.Email;

namespace Fixturely.IntegrationTests.Infrastructure;

/// <summary>
/// Test double for <see cref="IEmailSender"/> that captures every message instead of
/// delivering it over SMTP, so integration tests can extract confirmation/reset tokens
/// from the generated links without needing a real Mailpit instance.
/// </summary>
public sealed class TestEmailCapture : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _messages = new();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public EmailMessage? FindLatest(string recipientEmail, Fixturely.Domain.Enums.EmailTemplateType templateType) =>
        _messages.LastOrDefault(m =>
            m.ToEmail.Equals(recipientEmail, StringComparison.OrdinalIgnoreCase) && m.TemplateType == templateType);

    public IReadOnlyCollection<EmailMessage> Messages => _messages.ToArray();

    public EmailMessage? FindLatestFor(string recipientEmail) =>
        _messages.Where(m => m.ToEmail.Equals(recipientEmail, StringComparison.OrdinalIgnoreCase)).LastOrDefault();

    public void Clear()
    {
        while (_messages.TryDequeue(out _))
        {
        }
    }
}
