using Aria.Auth.Core.Models;

namespace Aria.Auth.Core.Services;

public sealed class IdentityProviderFactory
{
    private readonly Dictionary<string, IIdentityProvider> _providers;

    public IdentityProviderFactory(IEnumerable<IIdentityProvider> providers)
    {
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public IIdentityProvider Resolve(AriaConfig config)
    {
        var providerName = string.IsNullOrWhiteSpace(config.Auth.Provider)
            ? "entra"
            : config.Auth.Provider;

        if (!providerName.Equals("entra", StringComparison.OrdinalIgnoreCase)
            && !config.Auth.EnableExperimentalProviders)
        {
            throw new InvalidOperationException(
                $"Auth provider '{providerName}' is experimental. Set auth.enable_experimental_providers=true to enable it.");
        }

        if (_providers.TryGetValue(providerName, out var provider))
            return provider;

        throw new InvalidOperationException(
            $"Unknown auth provider '{providerName}'. Supported: {string.Join(", ", _providers.Keys.OrderBy(k => k))}");
    }
}
