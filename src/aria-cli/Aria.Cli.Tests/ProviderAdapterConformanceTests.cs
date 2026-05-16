using Aria.Auth.Core.Models;
using Aria.Auth.Core.Services;
using Aria.Auth.Core.TestFixtures;
using Aria.Cli.Services;
using Xunit;

namespace Aria.Cli.Tests;

public sealed class ProviderAdapterConformanceTests
{
    [Fact]
    public async Task SharedFixtures_AllAdaptersProduceNormalizedIdentityContract()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "provider-conformance-fixtures.json");
        var fixtures = ProviderConformanceFixtures.Load(fixturePath);

        foreach (var fixture in fixtures)
        {
            var token = fixture.BuildJwtToken();
            var identity = await ResolveWithProviderAsync(fixture.Provider, token);

            Assert.NotNull(identity);
            Assert.Equal(fixture.Expected.Provider, identity!.Provider);
            Assert.Equal(fixture.Expected.ObjectId, identity.ObjectId);
            Assert.Equal(fixture.Expected.TenantId, identity.TenantId);
            Assert.Equal(fixture.Expected.UserPrincipalName, identity.UserPrincipalName);

            foreach (var group in fixture.Expected.Groups)
                Assert.Contains(group, identity.Groups);

            foreach (var role in fixture.Expected.Roles)
                Assert.Contains(role, identity.Roles);
        }
    }

    private static async Task<ResolvedIdentity?> ResolveWithProviderAsync(string provider, string token)
    {
        return provider switch
        {
            "entra" => await ResolveEntraAsync(token),
            "okta" => await ResolveOktaAsync(token),
            "auth0" => await ResolveAuth0Async(token),
            _ => throw new InvalidOperationException($"Unsupported fixture provider '{provider}'.")
        };
    }

    private static async Task<ResolvedIdentity?> ResolveEntraAsync(string token)
    {
        var config = new AriaConfig
        {
            Entra = new EntraConfig { Enabled = true }
        };

        var provider = new EntraAuthService((_, _) => Task.FromResult(token));
        return await provider.GetIdentityAsync(config);
    }

    private static async Task<ResolvedIdentity?> ResolveOktaAsync(string token)
    {
        Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", token);

        try
        {
            var config = new AriaConfig
            {
                Okta = new OktaConfig
                {
                    Enabled = true,
                    Issuer = "https://example.okta.com"
                }
            };

            var provider = new OktaIdentityProvider();
            return await provider.GetIdentityAsync(config);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", null);
        }
    }

    private static async Task<ResolvedIdentity?> ResolveAuth0Async(string token)
    {
        Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", token);

        try
        {
            var config = new AriaConfig
            {
                Auth0 = new Auth0Config
                {
                    Enabled = true,
                    Domain = "example.auth0.com",
                    ClientId = "test-client-id"
                }
            };

            var provider = new Auth0IdentityProvider();
            return await provider.GetIdentityAsync(config);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", null);
        }
    }
}
