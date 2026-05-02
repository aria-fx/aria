using Aria.Auth.Core.Models;

namespace Aria.Auth.Core.Services;

public sealed record ResolvedIdentity(
    string Provider,
    string ObjectId,
    string TenantId,
    string? UserPrincipalName,
    HashSet<string> Groups,
    HashSet<string> Roles);

public interface IIdentityProvider
{
    string Name { get; }

    Task<ResolvedIdentity?> GetIdentityAsync(AriaConfig config);
}
