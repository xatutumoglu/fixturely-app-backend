namespace Fixturely.Application.Common;

public sealed class SessionOptions
{
    public int IdleTimeoutMinutes { get; set; } = 15;
}

public sealed class RefreshTokenOptions
{
    public int RefreshTokenDays { get; set; } = 7;
}
