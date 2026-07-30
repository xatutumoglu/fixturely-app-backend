using System.Text.RegularExpressions;
using Fixturely.Application.Abstractions.Email;

namespace Fixturely.IntegrationTests.Infrastructure;

public static class EmailLinkParser
{
    public static (Guid UserId, string Token) ParseUserIdAndToken(EmailMessage message)
    {
        var match = Regex.Match(message.HtmlBody, @"userId=(?<userId>[^&""]+)&amp;token=(?<token>[^""]+)");

        if (!match.Success)
        {
            match = Regex.Match(message.HtmlBody, @"userId=(?<userId>[^&""]+)&token=(?<token>[^""]+)");
        }

        var userId = Guid.Parse(Uri.UnescapeDataString(match.Groups["userId"].Value));
        var token = Uri.UnescapeDataString(match.Groups["token"].Value);

        return (userId, token);
    }

    public static string ParseInvitationToken(EmailMessage message)
    {
        var match = Regex.Match(message.HtmlBody, @"token=(?<token>[^&""]+)");
        return Uri.UnescapeDataString(match.Groups["token"].Value);
    }
}
