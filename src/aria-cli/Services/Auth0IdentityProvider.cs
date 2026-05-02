using Aria.Auth.Core.Models;
using Aria.Auth.Core.Services;

namespace Aria.Cli.Services;

/// <summary>
/// Stub adapter for Auth0.
/// TODO(auth-adapters): Implement token acquisition and claim normalization.
/// </summary>
public sealed class Auth0IdentityProvider : IIdentityProvider
{
    public string Name => "auth0";

    public Task<ResolvedIdentity?> GetIdentityAsync(AriaConfig config)
    {
        if (config.Auth0?.Enabled != true)
            return Task.FromResult<ResolvedIdentity?>(null);

        throw new NotSupportedException(
            "Auth0 provider is not implemented yet. Keep using auth.provider='entra' for production.");
    }
}
