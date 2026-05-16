using Aria.Auth.Core.Models;
using Aria.Auth.Core.Services;
using Aria.Auth.Core.TestFixtures;
using Xunit;

namespace Aria.Auth.Core.Tests;

public sealed class AccessPolicyConformanceTests
{
    [Fact]
    public async Task SharedFixtures_ResolveAccessPolicyConsistentlyAcrossProviders()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "provider-conformance-fixtures.json");
        var fixtures = ProviderConformanceFixtures.Load(fixturePath);

        foreach (var fixture in fixtures)
        {
            var identity = new ResolvedIdentity(
                fixture.Expected.Provider,
                fixture.Expected.ObjectId,
                fixture.Expected.TenantId,
                fixture.Expected.UserPrincipalName,
                fixture.Expected.Groups.ToHashSet(StringComparer.OrdinalIgnoreCase),
                fixture.Expected.Roles.ToHashSet(StringComparer.OrdinalIgnoreCase));

            var provider = new FixtureIdentityProvider(fixture.Provider, identity);
            var factory = new IdentityProviderFactory([provider]);
            var service = new AccessPolicyService(factory);

            var config = CreateConfigFor(fixture.Provider);
            var context = await service.ResolveAsync(config);

            Assert.NotNull(context.Identity);
            Assert.Equal(fixture.Expected.ObjectId, context.Identity!.ObjectId);
            Assert.Equal("restricted", context.SensitivityCeiling);
            Assert.Contains("finance-group-rule", context.MatchedRules);
            Assert.Contains("asset-reader-rule", context.MatchedRules);
            Assert.Contains("Data Reader", context.PurviewRoles);
            Assert.Contains("Data Curator", context.PurviewRoles);
        }
    }

    private static AriaConfig CreateConfigFor(string provider)
    {
        return new AriaConfig
        {
            SensitivityCeiling = "public",
            Auth = new AuthConfig
            {
                Provider = provider,
                EnableExperimentalProviders = true
            },
            AccessRules =
            [
                new AccessRule
                {
                    Name = "finance-group-rule",
                    AnyEntraGroups = ["finance-team"],
                    SensitivityCeiling = "confidential",
                    PurviewRoles = ["Data Reader"]
                },
                new AccessRule
                {
                    Name = "asset-reader-rule",
                    AnyEntraRoles = ["Asset.Reader"],
                    SensitivityCeiling = "restricted",
                    PurviewRoles = ["Data Curator"]
                }
            ]
        };
    }

    private sealed class FixtureIdentityProvider : IIdentityProvider
    {
        private readonly ResolvedIdentity _identity;

        public FixtureIdentityProvider(string name, ResolvedIdentity identity)
        {
            Name = name;
            _identity = identity;
        }

        public string Name { get; }

        public Task<ResolvedIdentity?> GetIdentityAsync(AriaConfig config)
        {
            return Task.FromResult<ResolvedIdentity?>(_identity);
        }
    }
}
