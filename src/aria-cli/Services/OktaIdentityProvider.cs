using Aria.Auth.Core.Models;
using Aria.Auth.Core.Services;
using System.Text;
using System.Text.Json;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Aria.Cli.Services;

/// <summary>
/// Stub adapter for Okta.
/// TODO(auth-adapters): Implement OIDC device/browser login and claim normalization for groups/roles.
/// </summary>
public sealed class OktaIdentityProvider : IIdentityProvider
{
    public string Name => "okta";

    public async Task<ResolvedIdentity?> GetIdentityAsync(AriaConfig config)
    {
        var okta = config.Okta;
        if (okta?.Enabled != true)
            return null;

        var token = await ResolveAccessTokenAsync(okta);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "Okta is enabled but no access token source is available. Set access_token_env_var, access_token_file, or token_endpoint.");
        }

        return ParseJwtIdentity(token, okta);
    }

    private static async Task<string?> ResolveAccessTokenAsync(OktaConfig okta)
    {
        var envTokenName = string.IsNullOrWhiteSpace(okta.AccessTokenEnvVar)
            ? "OKTA_ACCESS_TOKEN"
            : okta.AccessTokenEnvVar;

        var envToken = Environment.GetEnvironmentVariable(envTokenName);
        if (!string.IsNullOrWhiteSpace(envToken))
            return envToken;

        if (!string.IsNullOrWhiteSpace(okta.AccessTokenFile))
        {
            var expandedPath = ExpandTildePath(okta.AccessTokenFile);
            if (File.Exists(expandedPath))
            {
                var fileToken = (await File.ReadAllTextAsync(expandedPath)).Trim();
                if (!string.IsNullOrWhiteSpace(fileToken))
                    return fileToken;
            }
        }

        if (!string.IsNullOrWhiteSpace(okta.TokenEndpoint) && !string.IsNullOrWhiteSpace(okta.ClientId))
            return await RequestClientCredentialsTokenAsync(okta);

        return null;
    }

    private static async Task<string?> RequestClientCredentialsTokenAsync(OktaConfig okta)
    {
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, okta.TokenEndpoint);

        var secretEnvName = string.IsNullOrWhiteSpace(okta.ClientSecretEnvVar)
            ? "OKTA_CLIENT_SECRET"
            : okta.ClientSecretEnvVar;
        var clientSecret = Environment.GetEnvironmentVariable(secretEnvName) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                $"Okta token endpoint is configured but no client secret was found in env var '{secretEnvName}'.");
        }

        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{okta.ClientId}:{clientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);

        var scope = okta.Scopes.Count > 0
            ? string.Join(' ', okta.Scopes)
            : "openid profile groups";

        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = scope
        });

        using var response = await http.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"Okta token request failed: {(int)response.StatusCode} {response.ReasonPhrase}");

        using var doc = JsonDocument.Parse(payload);
        if (doc.RootElement.TryGetProperty("access_token", out var accessToken)
            && accessToken.ValueKind == JsonValueKind.String)
        {
            return accessToken.GetString();
        }

        throw new InvalidOperationException("Okta token response did not include access_token.");
    }

    private static ResolvedIdentity ParseJwtIdentity(string jwt, OktaConfig okta)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            throw new InvalidOperationException("Okta access token is not a JWT. Provide a JWT access token.");

        string payloadJson;
        try
        {
            payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Okta access token has invalid base64 encoding.", ex);
        }

        using var payload = JsonDocument.Parse(payloadJson);
        var root = payload.RootElement;

        var objectId = GetFirstStringClaim(root, "oid", "uid", "sub")
            ?? throw new InvalidOperationException("Okta token missing identity claim (oid|uid|sub).");
        var tenantId = GetFirstStringClaim(root, "tid")
            ?? okta.Issuer
            ?? GetFirstStringClaim(root, "iss")
            ?? string.Empty;
        var upn = GetFirstStringClaim(root, "preferred_username", "upn", "email", "sub");

        var groups = ReadClaims(root, "groups", "group", "okta.groups");
        var roles = ReadClaims(root, "roles", "role", "permissions", "scp", "scope");

        return new ResolvedIdentity("okta", objectId, tenantId, upn, groups, roles);
    }

    private static string? GetFirstStringClaim(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var claim) && claim.ValueKind == JsonValueKind.String)
                return claim.GetString();
        }

        return null;
    }

    private static HashSet<string> ReadClaims(JsonElement root, params string[] names)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var claim))
                continue;

            switch (claim.ValueKind)
            {
                case JsonValueKind.Array:
                    foreach (var item in claim.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            AddSplit(values, item.GetString());
                    }
                    break;
                case JsonValueKind.String:
                    AddSplit(values, claim.GetString());
                    break;
            }
        }

        return values;
    }

    private static void AddSplit(HashSet<string> values, string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return;

        foreach (var token in input.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            values.Add(token);
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        var mod = padded.Length % 4;
        if (mod > 0)
            padded = padded.PadRight(padded.Length + (4 - mod), '=');
        return Convert.FromBase64String(padded);
    }

    private static string ExpandTildePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        if (path.StartsWith("~/") || path == "~")
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (string.IsNullOrWhiteSpace(home))
                home = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;

            return path == "~" ? home : Path.Combine(home, path.Substring(2));
        }

        return path;
    }
}
