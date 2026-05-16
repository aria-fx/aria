using System.Text;
using System.Text.Json;

namespace Aria.Auth.Core.TestFixtures;

public sealed record ExpectedNormalizedIdentity(
    string Provider,
    string ObjectId,
    string TenantId,
    string? UserPrincipalName,
    List<string> Groups,
    List<string> Roles);

public sealed record ProviderFixtureCase(
    string Name,
    string Provider,
    Dictionary<string, JsonElement> Claims,
    ExpectedNormalizedIdentity Expected)
{
    public string BuildJwtToken()
    {
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = "https://fixtures.aria.dev",
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        };

        foreach (var (key, value) in Claims)
            payload[key] = JsonToClr(value);

        var header = new Dictionary<string, object?>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT"
        };

        var headerEncoded = Base64UrlEncode(JsonSerializer.Serialize(header));
        var payloadEncoded = Base64UrlEncode(JsonSerializer.Serialize(payload));
        var signature = Base64UrlEncode("fixture-signature");

        return $"{headerEncoded}.{payloadEncoded}.{signature}";
    }

    private static object? JsonToClr(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.TryGetInt64(out var i) ? i : value.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Array => value.EnumerateArray().Select(JsonToClr).ToList(),
            JsonValueKind.Object => value.EnumerateObject().ToDictionary(p => p.Name, p => JsonToClr(p.Value)),
            _ => value.ToString()
        };
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }
}

public static class ProviderConformanceFixtures
{
    public static IReadOnlyList<ProviderFixtureCase> Load(string fixturePath)
    {
        var json = File.ReadAllText(fixturePath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var fixtures = new List<ProviderFixtureCase>();
        foreach (var item in root.GetProperty("fixtures").EnumerateArray())
        {
            var claims = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var claim in item.GetProperty("claims").EnumerateObject())
                claims[claim.Name] = claim.Value.Clone();

            var expectedElement = item.GetProperty("expected");
            var expected = new ExpectedNormalizedIdentity(
                expectedElement.GetProperty("provider").GetString() ?? string.Empty,
                expectedElement.GetProperty("objectId").GetString() ?? string.Empty,
                expectedElement.GetProperty("tenantId").GetString() ?? string.Empty,
                expectedElement.TryGetProperty("userPrincipalName", out var upn) ? upn.GetString() : null,
                expectedElement.GetProperty("groups").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList(),
                expectedElement.GetProperty("roles").EnumerateArray().Select(x => x.GetString() ?? string.Empty).ToList());

            fixtures.Add(new ProviderFixtureCase(
                item.GetProperty("name").GetString() ?? string.Empty,
                item.GetProperty("provider").GetString() ?? string.Empty,
                claims,
                expected));
        }

        return fixtures;
    }
}
