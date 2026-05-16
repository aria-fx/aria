using System.Text;
using System.Text.Json;
using Aria.Auth.Core.Models;
using Aria.Cli.Services;
using Xunit;

namespace Aria.Cli.Tests;

public sealed class OktaIdentityProviderTests
{
    private const string TestOktaIssuer = "https://example.okta.com";

    [Fact]
    public void Provider_Name_ReturnsOkta()
    {
        var provider = new OktaIdentityProvider();
        Assert.Equal("okta", provider.Name);
    }

    [Fact]
    public async Task GetIdentity_OktaDisabled_ReturnsNull()
    {
        var provider = new OktaIdentityProvider();
        var config = new AriaConfig
        {
            Okta = new OktaConfig { Enabled = false }
        };

        var result = await provider.GetIdentityAsync(config);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetIdentity_NoOktaConfig_ReturnsNull()
    {
        var provider = new OktaIdentityProvider();
        var config = new AriaConfig();

        var result = await provider.GetIdentityAsync(config);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetIdentity_OktaEnabledNoTokenSource_Throws()
    {
        var provider = new OktaIdentityProvider();
        var config = new AriaConfig
        {
            Okta = new OktaConfig { Enabled = true }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.GetIdentityAsync(config));

        Assert.Contains("no access token source", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetIdentity_ValidTokenFromEnvVar_ParsesIdentity()
    {
        // Arrange
        var token = CreateTestJwt();
        Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", token);

        try
        {
            var provider = new OktaIdentityProvider();
            var config = new AriaConfig
            {
                Okta = new OktaConfig
                {
                    Enabled = true,
                    Issuer = TestOktaIssuer
                }
            };

            // Act
            var result = await provider.GetIdentityAsync(config);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("okta", result.Provider);
            Assert.Equal("user123", result.ObjectId);
            Assert.NotEmpty(result.Groups);
            Assert.NotEmpty(result.Roles);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetIdentity_ValidTokenFromFile_ParsesIdentity()
    {
        // Arrange
        var token = CreateTestJwt();
        var tempFile = Path.Combine(Path.GetTempPath(), "okta_token.txt");
        await File.WriteAllTextAsync(tempFile, token);

        try
        {
            var provider = new OktaIdentityProvider();
            var config = new AriaConfig
            {
                Okta = new OktaConfig
                {
                    Enabled = true,
                    AccessTokenFile = tempFile,
                    Issuer = TestOktaIssuer
                }
            };

            // Act
            var result = await provider.GetIdentityAsync(config);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user123", result.ObjectId);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task GetIdentity_InvalidJwtFormat_Throws()
    {
        // Arrange
        Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", "not.a.jwt");

        try
        {
            var provider = new OktaIdentityProvider();
            var config = new AriaConfig
            {
                Okta = new OktaConfig
                {
                    Enabled = true,
                    Issuer = TestOktaIssuer
                }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetIdentityAsync(config));
            Assert.Contains("invalid base64", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetIdentity_TokenWithoutIdentityClaim_Throws()
    {
        // Arrange
        var token = CreateTestJwtWithClaims(new Dictionary<string, object>
        {
            ["email"] = "user@example.com",
            ["groups"] = new[] { "group1" }
        });
        Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", token);

        try
        {
            var provider = new OktaIdentityProvider();
            var config = new AriaConfig
            {
                Okta = new OktaConfig
                {
                    Enabled = true,
                    Issuer = TestOktaIssuer
                }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetIdentityAsync(config));
            Assert.Contains("identity claim", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetIdentity_TokenWithOidClaim_ParsesCorrectly()
    {
        // Arrange
        var token = CreateTestJwtWithClaims(new Dictionary<string, object>
        {
            ["oid"] = "okta-object-id",
            ["uid"] = "okta-user-id",
            ["email"] = "user@example.com",
            ["preferred_username"] = "user@example.com",
            ["groups"] = new[] { "admins", "developers" },
            ["roles"] = new[] { "admin", "user" }
        });
        Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", token);

        try
        {
            var provider = new OktaIdentityProvider();
            var config = new AriaConfig
            {
                Okta = new OktaConfig
                {
                    Enabled = true,
                    Issuer = TestOktaIssuer
                }
            };

            // Act
            var result = await provider.GetIdentityAsync(config);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("okta-object-id", result.ObjectId); // "oid" takes precedence
            Assert.Equal("user@example.com", result.UserPrincipalName);
            Assert.Equal(2, result.Groups.Count);
            Assert.Contains("admins", result.Groups);
            Assert.Equal(2, result.Roles.Count);
            Assert.Contains("admin", result.Roles);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetIdentity_TokenWithScopeClaim_ParsesAsRoles()
    {
        // Arrange
        var token = CreateTestJwtWithClaims(new Dictionary<string, object>
        {
            ["sub"] = "user456",
            ["scp"] = "openid profile email read:api write:api" // Okta scope format
        });
        Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", token);

        try
        {
            var provider = new OktaIdentityProvider();
            var config = new AriaConfig
            {
                Okta = new OktaConfig
                {
                    Enabled = true,
                    Issuer = TestOktaIssuer
                }
            };

            // Act
            var result = await provider.GetIdentityAsync(config);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("read:api", result.Roles);
            Assert.Contains("write:api", result.Roles);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetIdentity_TokenWithOktaGroupsClaim_ParsesCorrectly()
    {
        // Arrange
        var token = CreateTestJwtWithClaims(new Dictionary<string, object>
        {
            ["uid"] = "user789",
            ["okta.groups"] = new[] { "okta-admins", "okta-developers" }
        });
        Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", token);

        try
        {
            var provider = new OktaIdentityProvider();
            var config = new AriaConfig
            {
                Okta = new OktaConfig
                {
                    Enabled = true,
                    Issuer = TestOktaIssuer
                }
            };

            // Act
            var result = await provider.GetIdentityAsync(config);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("okta-admins", result.Groups);
            Assert.Contains("okta-developers", result.Groups);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetIdentity_TenantIdFromConfigIssuer_ParsesCorrectly()
    {
        // Arrange
        var token = CreateTestJwtWithClaims(new Dictionary<string, object>
        {
            ["sub"] = "user999"
            // No "iss" in token; should use config Issuer
        });
        Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", token);

        try
        {
            var provider = new OktaIdentityProvider();
            var config = new AriaConfig
            {
                Okta = new OktaConfig
                {
                    Enabled = true,
                    Issuer = TestOktaIssuer
                }
            };

            // Act
            var result = await provider.GetIdentityAsync(config);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(TestOktaIssuer, result.TenantId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OKTA_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetIdentity_TokenFromTildeExpandedFile_ParsesIdentity()
    {
        // Arrange
        var token = CreateTestJwt();
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(homeDir))
            homeDir = Environment.GetEnvironmentVariable("HOME") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(homeDir))
        {
            // Skip test if home directory cannot be determined
            return;
        }

        var testDir = Path.Combine(homeDir, ".aria-test-okta-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        var tokenFile = Path.Combine(testDir, "okta-token.txt");
        await File.WriteAllTextAsync(tokenFile, token);

        try
        {
            var provider = new OktaIdentityProvider();
            var relativePath = "~/" + Path.GetRelativePath(homeDir, tokenFile);
            var config = new AriaConfig
            {
                Okta = new OktaConfig
                {
                    Enabled = true,
                    Issuer = TestOktaIssuer,
                    AccessTokenFile = relativePath
                }
            };

            // Act
            var result = await provider.GetIdentityAsync(config);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("user123", result.ObjectId);
        }
        finally
        {
            if (Directory.Exists(testDir))
                Directory.Delete(testDir, true);
        }
    }

    private static string CreateTestJwt() =>
        CreateTestJwtWithClaims(new Dictionary<string, object>
        {
            ["uid"] = "user123",
            ["sub"] = "user123",
            ["email"] = "user@example.com",
            ["preferred_username"] = "user@example.com",
            ["groups"] = new[] { "group1", "group2" },
            ["roles"] = new[] { "role1", "role2" }
        });

    private static string CreateTestJwtWithClaims(Dictionary<string, object> claims)
    {
        // Create header
        var header = new Dictionary<string, string>
        {
            ["alg"] = "RS256",
            ["typ"] = "JWT"
        };

        // Create payload
        var payload = new Dictionary<string, object>(claims)
        {
            ["iss"] = TestOktaIssuer,
            ["aud"] = "test-audience",
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()
        };

        // Encode
        var headerJson = JsonSerializer.Serialize(header);
        var payloadJson = JsonSerializer.Serialize(payload);

        var headerEncoded = Base64UrlEncode(headerJson);
        var payloadEncoded = Base64UrlEncode(payloadJson);

        // For testing, we use a dummy signature (real validation would need proper key)
        var signature = Base64UrlEncode("dummy-signature");

        return $"{headerEncoded}.{payloadEncoded}.{signature}";
    }

    private static string Base64UrlEncode(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}
