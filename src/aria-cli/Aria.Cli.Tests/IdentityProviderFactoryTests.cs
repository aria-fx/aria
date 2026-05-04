using Aria.Auth.Core.Models;
using Aria.Auth.Core.Services;
using Aria.Cli.Services;
using Xunit;

namespace Aria.Cli.Tests;

public sealed class IdentityProviderFactoryTests
{
    [Fact]
    public void Resolve_DefaultProvider_UsesEntra()
    {
        var factory = CreateFactory();
        var config = new AriaConfig();

        var provider = factory.Resolve(config);

        Assert.Equal("entra", provider.Name);
    }

    [Fact]
    public void Resolve_ExperimentalProviderDisabled_Throws()
    {
        var factory = CreateFactory();
        var config = new AriaConfig
        {
            Auth = new AuthConfig
            {
                Provider = "okta",
                EnableExperimentalProviders = false
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => factory.Resolve(config));

        Assert.Contains("experimental", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_ExperimentalProviderEnabled_ReturnsProvider()
    {
        var factory = CreateFactory();
        var config = new AriaConfig
        {
            Auth = new AuthConfig
            {
                Provider = "okta",
                EnableExperimentalProviders = true
            }
        };

        var provider = factory.Resolve(config);

        Assert.Equal("okta", provider.Name);
    }

    [Fact]
    public void Resolve_UnknownProvider_Throws()
    {
        var factory = CreateFactory();
        var config = new AriaConfig
        {
            Auth = new AuthConfig
            {
                Provider = "unknown",
                EnableExperimentalProviders = true
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() => factory.Resolve(config));

        Assert.Contains("Unknown auth provider", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IdentityProviderFactory CreateFactory() =>
        new([
            new EntraAuthService(),
            new OktaIdentityProvider(),
            new Auth0IdentityProvider()
        ]);
}
