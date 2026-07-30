using Fixturely.Domain.Common;
using Fixturely.Domain.Enums;

namespace Fixturely.Domain.Entities;

public sealed class EmailDeliveryEvent : Entity
{
    private EmailDeliveryEvent()
    {
    }

    public Guid? UserId { get; private set; }

    public string RecipientEmail { get; private set; } = string.Empty;

    public EmailTemplateType TemplateType { get; private set; }

    public EmailDeliveryStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTime AttemptedAtUtc { get; private set; }

    public static EmailDeliveryEvent CreateSent(
        Guid? userId,
        string recipientEmail,
        EmailTemplateType templateType,
        DateTime utcNow)
    {
        var evt = new EmailDeliveryEvent
        {
            UserId = userId,
            RecipientEmail = recipientEmail,
            TemplateType = templateType,
            Status = EmailDeliveryStatus.Sent,
            AttemptedAtUtc = utcNow
        };
        evt.Initialize(utcNow);
        return evt;
    }

    public static EmailDeliveryEvent CreateFailed(
        Guid? userId,
        string recipientEmail,
        EmailTemplateType templateType,
        string failureReason,
        DateTime utcNow)
    {
        var evt = new EmailDeliveryEvent
        {
            UserId = userId,
            RecipientEmail = recipientEmail,
            TemplateType = templateType,
            Status = EmailDeliveryStatus.Failed,
            FailureReason = failureReason,
            AttemptedAtUtc = utcNow
        };
        evt.Initialize(utcNow);
        return evt;
    }
}
