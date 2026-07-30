namespace Fixturely.Infrastructure.Options;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "Fixturely";

    public string Audience { get; set; } = "FixturelyClient";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 7;
}
