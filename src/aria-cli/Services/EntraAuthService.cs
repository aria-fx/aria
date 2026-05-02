// ─────────────────────────────────────────────────────────────
// Services/EntraAuthService.cs
// Resolves Microsoft Entra identity for the current CLI user
// using DefaultAzureCredential and token claim extraction.
// ─────────────────────────────────────────────────────────────

using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Aria.Auth.Core.Models;
using Aria.Auth.Core.Services;

namespace Aria.Cli.Services;

/// <summary>
/// Entra implementation of the identity provider adapter.
/// TODO(auth-adapters): Keep Entra first-class, but avoid Entra-specific assumptions in downstream policy code.
/// </summary>
public sealed class EntraAuthService : IIdentityProvider
{
    public string Name => "entra";

    public async Task<ResolvedIdentity?> GetIdentityAsync(AriaConfig config)
    {
        if (config.Entra?.Enabled != true)
            return null;

        var options = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(config.Entra.TenantId))
            options.TenantId = config.Entra.TenantId;
        if (!string.IsNullOrWhiteSpace(config.Entra.ClientId))
            options.ManagedIdentityClientId = config.Entra.ClientId;

        var credential = new DefaultAzureCredential(options);
        var scopes = config.Entra.Scopes.Count > 0
            ? config.Entra.Scopes.ToArray()
            : new[] { "https://management.azure.com/.default" };

        AccessToken token = await credential.GetTokenAsync(
            new TokenRequestContext(scopes),
            CancellationToken.None);

        return ParseJwtIdentity(token.Token);
    }

    private static ResolvedIdentity ParseJwtIdentity(string jwt)
    {
        var parts = jwt.Split('.');
        if (parts.Length < 2)
            throw new InvalidOperationException("Entra token is not a valid JWT.");

        var payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
        using var payload = JsonDocument.Parse(payloadJson);

        var root = payload.RootElement;

        var objectId = GetClaim(root, "oid")
            ?? throw new InvalidOperationException("Token missing 'oid' claim.");
        var tenantId = GetClaim(root, "tid") ?? "";
        var upn = GetClaim(root, "preferred_username")
                  ?? GetClaim(root, "upn")
                  ?? GetClaim(root, "email");

        var groups = ReadClaimArray(root, "groups");
        var roles = ReadClaimArray(root, "roles");
        var scopes = GetClaim(root, "scp");
        if (!string.IsNullOrWhiteSpace(scopes))
        {
            foreach (var scope in scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                roles.Add(scope);
        }

        return new ResolvedIdentity("entra", objectId, tenantId, upn, groups, roles);
    }

    private static string? GetClaim(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out var claim) && claim.ValueKind == JsonValueKind.String)
            return claim.GetString();
        return null;
    }

    private static HashSet<string> ReadClaimArray(JsonElement root, string claimName)
    {
        var values = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(claimName, out var claim) || claim.ValueKind != JsonValueKind.Array)
            return values;

        foreach (var item in claim.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var value = item.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                    values.Add(value);
            }
        }

        return values;
    }

    private static byte[] Base64UrlDecode(string value)
    {
        var padded = value.Replace('-', '+').Replace('_', '/');
        var mod = padded.Length % 4;
        if (mod > 0)
            padded = padded.PadRight(padded.Length + (4 - mod), '=');
        return Convert.FromBase64String(padded);
    }
}
