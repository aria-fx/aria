using System.Text;
using System.Text.Json;
using System.Net.Http;
using Aria.Auth.Core.Models;
using Aria.Auth.Core.Services;

namespace Aria.Cli.Services;

/// <summary>
/// Auth0 implementation of the identity provider adapter.
/// Supports device flow for interactive CLI use and static tokens for CI/CD.
/// </summary>
public sealed class Auth0IdentityProvider : IIdentityProvider
{
    private static readonly HttpClient SharedHttpClient = new();
    private readonly HttpClient _httpClient;

    public Auth0IdentityProvider()
        : this(SharedHttpClient)
    {
    }

    public Auth0IdentityProvider(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public string Name => "auth0";

    public async Task<ResolvedIdentity?> GetIdentityAsync(AriaConfig config)
    {
        var auth0 = config.Auth0;
        if (auth0?.Enabled != true)
            return null;

        var token = await ResolveAccessTokenAsync(auth0);
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "Auth0 is enabled but no access token source is available. Set AUTH0_ACCESS_TOKEN, configure AccessTokenFile, or provide Auth0 domain and client_id for device flow.");
        }

        return ParseJwtIdentity(token, auth0);
    }

    private async Task<string?> ResolveAccessTokenAsync(Auth0Config auth0)
    {
        // Try environment variable first
        var envToken = Environment.GetEnvironmentVariable("AUTH0_ACCESS_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken))
            return envToken;

        // Try file-based token
        if (!string.IsNullOrWhiteSpace(auth0.AccessTokenFile))
        {
            var expandedPath = PathHelper.ExpandTildePath(auth0.AccessTokenFile);
            if (File.Exists(expandedPath))
            {
                var fileToken = (await File.ReadAllTextAsync(expandedPath)).Trim();
                if (!string.IsNullOrWhiteSpace(fileToken))
                    return fileToken;
            }
        }

        // Use device flow if domain and client_id are configured
        if (!string.IsNullOrWhiteSpace(auth0.Domain) && !string.IsNullOrWhiteSpace(auth0.ClientId))
        {
            return await RequestDeviceFlowTokenAsync(auth0);
        }

        return null;
    }

    private async Task<string?> RequestDeviceFlowTokenAsync(Auth0Config auth0)
    {
        var domain = auth0.Domain!;
        var clientId = auth0.ClientId!;
        var audience = auth0.Audience ?? $"https://{domain}/api/v2/";
        var scope = auth0.Scopes.Count > 0
            ? string.Join(' ', auth0.Scopes)
            : "openid profile email offline_access";

        // Step 1: Request device code
        var deviceCodeUrl = $"https://{domain}/oauth/device/code";
        using var deviceRequest = new HttpRequestMessage(HttpMethod.Post, deviceCodeUrl);
        deviceRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["scope"] = scope,
            ["audience"] = audience
        });

        using var deviceResponse = await _httpClient.SendAsync(deviceRequest);
        var devicePayload = await deviceResponse.Content.ReadAsStringAsync();

        if (!deviceResponse.IsSuccessStatusCode)
        {
            var message = $"Auth0 device code request failed: {(int)deviceResponse.StatusCode}";

            if (!string.IsNullOrWhiteSpace(deviceResponse.ReasonPhrase))
                message += $" ({deviceResponse.ReasonPhrase})";

            if (!string.IsNullOrWhiteSpace(devicePayload))
                message += $". Response: {devicePayload}";

            throw new InvalidOperationException(message);
        }

        using var deviceDoc = JsonDocument.Parse(devicePayload);
        var deviceRoot = deviceDoc.RootElement;

        var deviceCode = GetStringProperty(deviceRoot, "device_code")
            ?? throw new InvalidOperationException("Auth0 device code response missing device_code.");
        var userCode = GetStringProperty(deviceRoot, "user_code")
            ?? throw new InvalidOperationException("Auth0 device code response missing user_code.");
        var verificationUri = GetStringProperty(deviceRoot, "verification_uri")
            ?? throw new InvalidOperationException("Auth0 device code response missing verification_uri.");
        var expiresIn = GetIntProperty(deviceRoot, "expires_in") ?? 900;
        var pollInterval = GetIntProperty(deviceRoot, "interval") ?? 5;

        // Step 2: Prompt user to authorize
        Console.WriteLine();
        Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║                  Auth0 Device Authorization                 ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
        Console.WriteLine($"Visit this URL in your browser:");
        Console.WriteLine($"  {verificationUri}");
        Console.WriteLine();
        Console.WriteLine($"Enter this code when prompted:");
        Console.WriteLine($"  {userCode}");
        Console.WriteLine();

        // Step 3: Poll for token
        var tokenUrl = $"https://{domain}/oauth/token";
        var deadline = DateTime.UtcNow.AddSeconds(expiresIn);

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(pollInterval * 1000);

            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
            tokenRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["device_code"] = deviceCode,
                ["grant_type"] = "urn:ietf:params:oauth:grant-type:device_code"
            });

            using var tokenResponse = await _httpClient.SendAsync(tokenRequest);
            var tokenPayload = await tokenResponse.Content.ReadAsStringAsync();

            if (!tokenResponse.IsSuccessStatusCode)
            {
                // Check for specific error types
                using var errorDoc = JsonDocument.Parse(tokenPayload);
                var errorCode = GetStringProperty(errorDoc.RootElement, "error");

                if (errorCode == "authorization_pending")
                {
                    // User hasn't authorized yet, keep polling
                    continue;
                }

                if (errorCode == "slow_down")
                {
                    // Increase polling interval
                    pollInterval = Math.Min(pollInterval + 5, 120);
                    continue;
                }

                if (errorCode == "expired_token")
                {
                    throw new InvalidOperationException(
                        "Device code expired. Please try again.");
                }

                throw new InvalidOperationException(
                    $"Auth0 token request failed: {errorCode} - {GetStringProperty(errorDoc.RootElement, "error_description")}");
            }

            using var successDoc = JsonDocument.Parse(tokenPayload);
            var accessToken = GetStringProperty(successDoc.RootElement, "access_token");
            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                Console.WriteLine("✓ Authorization successful!");
                Console.WriteLine();
                return accessToken;
            }
        }

        throw new InvalidOperationException(
            "Device authorization timeout. Please try again.");
    }

    private static ResolvedIdentity ParseJwtIdentity(string jwt, Auth0Config auth0)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            throw new InvalidOperationException("Auth0 access token is not a JWT. Provide a JWT access token.");

        string payloadJson;
        try
        {
            payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException("Auth0 access token has invalid base64 encoding.", ex);
        }

        using var payload = JsonDocument.Parse(payloadJson);
        var root = payload.RootElement;

        var objectId = GetFirstStringClaim(root, "sub", "oid", "uid")
            ?? throw new InvalidOperationException("Auth0 token missing identity claim (sub|oid|uid).");

        var tenantId = GetFirstStringClaim(root, "aud", "iss")
            ?? auth0.Domain
            ?? string.Empty;

        var upn = GetFirstStringClaim(root, "preferred_username", "email", "nickname", "name");

        var groups = ReadClaims(root, "groups", "org_id", "https://aria.dev/groups");
        var roles = ReadClaims(root, "roles", "permissions", "scope", "https://aria.dev/roles");

        return new ResolvedIdentity("auth0", objectId, tenantId, upn, groups, roles);
    }

    private static string? GetFirstStringClaim(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var claim))
                continue;

            // Handle string claims
            if (claim.ValueKind == JsonValueKind.String)
                return claim.GetString();

            // Handle array claims (e.g., aud can be an array in Auth0)
            // Returns the first string element from the array
            if (claim.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in claim.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                        return item.GetString();
                }
            }
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

    private static string? GetStringProperty(JsonElement root, string name) =>
        root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static int? GetIntProperty(JsonElement root, string name) =>
        root.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32()
            : null;
}
