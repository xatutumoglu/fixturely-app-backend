namespace Fixturely.Infrastructure.Options;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 587;

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string FromEmail { get; set; } = "no-reply@fixturely.local";

    public string FromName { get; set; } = "Fixturely";

    public bool UseSsl { get; set; }
}

public sealed class FrontendOptions
{
    public string BaseUrl { get; set; } = "http://localhost:5173";
}
