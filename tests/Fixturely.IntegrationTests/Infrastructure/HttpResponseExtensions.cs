using System.Net.Http.Json;
using System.Text.Json;

namespace Fixturely.IntegrationTests.Infrastructure;

public static class HttpResponseExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<T?> ReadAsAsync<T>(this HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions);
    }

    public static string? GetSetCookieValue(this HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders))
        {
            return null;
        }

        foreach (var header in cookieHeaders)
        {
            if (header.StartsWith($"{cookieName}=", StringComparison.OrdinalIgnoreCase))
            {
                var withoutName = header[(cookieName.Length + 1)..];
                var semicolonIndex = withoutName.IndexOf(';');
                return semicolonIndex >= 0 ? withoutName[..semicolonIndex] : withoutName;
            }
        }

        return null;
    }
}
