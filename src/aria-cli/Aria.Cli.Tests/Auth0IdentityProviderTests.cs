using System.Text;
using System.Text.Json;
using Aria.Auth.Core.Models;
using Aria.Cli.Services;
using Xunit;

namespace Aria.Cli.Tests;

public sealed class Auth0IdentityProviderTests
{
    private const string TestAuth0Domain = "example.auth0.com";
    private const string TestClientId = "test-client-id";

    [Fact]
    public void Provider_Name_ReturnsAuth0()
    {
        var provider = new Auth0IdentityProvider();
        Assert.Equal("auth0", provider.Name);
    }

    [Fact]
    public async Task GetIdentity_Auth0Disabled_ReturnsNull()
    {
        var provider = new Auth0IdentityProvider();
        var config = new AriaConfig
        {
            Auth0 = new Auth0Config { Enabled = false }
        };

        var result = await provider.GetIdentityAsync(config);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetIdentity_NoAuth0Config_ReturnsNull()
    {
        var provider = new Auth0IdentityProvider();
        var config = new AriaConfig();

        var result = await provider.GetIdentityAsync(config);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetIdentity_Auth0EnabledNoTokenSource_Throws()
    {
        var provider = new Auth0IdentityProvider();
        var config = new AriaConfig
        {
            Auth0 = new Auth0Config { Enabled = true }
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
        Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", token);

        try
        {
            var provider = new Auth0IdentityProvider();
            var config = new AriaConfig
            {
                Auth0 = new Auth0Config
                {
                    Enabled = true,
                    Domain = TestAuth0Domain,
                    ClientId = TestClientId
                }
            };

            // Act
            var result = await provider.GetIdentityAsync(config);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("auth0", result.Provider);
            Assert.Equal("user123", result.ObjectId);
            Assert.NotEmpty(result.Groups);
            Assert.NotEmpty(result.Roles);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetIdentity_ValidTokenFromFile_ParsesIdentity()
    {
        // Arrange
        var token = CreateTestJwt();
        var tempFile = Path.Combine(Path.GetTempPath(), "auth0_token.txt");
        await File.WriteAllTextAsync(tempFile, token);

        try
        {
            var provider = new Auth0IdentityProvider();
            var config = new AriaConfig
            {
                Auth0 = new Auth0Config
                {
                    Enabled = true,
                    Domain = TestAuth0Domain,
                    ClientId = TestClientId,
                    AccessTokenFile = tempFile
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
        Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", "not.a.jwt");

        try
        {
            var provider = new Auth0IdentityProvider();
            var config = new AriaConfig
            {
                Auth0 = new Auth0Config
                {
                    Enabled = true,
                    Domain = TestAuth0Domain
                }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetIdentityAsync(config));
            Assert.Contains("invalid base64", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetIdentity_TokenWithoutSubClaim_Throws()
    {
        // Arrange
        var token = CreateTestJwtWithClaims(new Dictionary<string, object>
        {
            ["email"] = "user@example.com",
            ["groups"] = new[] { "group1" }
        });
        Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", token);

        try
        {
            var provider = new Auth0IdentityProvider();
            var config = new AriaConfig
            {
                Auth0 = new Auth0Config
                {
                    Enabled = true,
                    Domain = TestAuth0Domain
                }
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => provider.GetIdentityAsync(config));
            Assert.Contains("identity claim", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetIdentity_TokenWithGroupsAndRoles_ParsesCorrectly()
    {
        // Arrange
        var token = CreateTestJwtWithClaims(new Dictionary<string, object>
        {
            ["sub"] = "auth0|user456",
            ["email"] = "user@example.com",
            ["groups"] = new[] { "admins", "developers", "infrastructure" },
            ["roles"] = new[] { "admin", "contributor" },
            ["https://aria.dev/groups"] = new[] { "aria-admins" }
        });
        Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", token);

        try
        {
            var provider = new Auth0IdentityProvider();
            var config = new AriaConfig
            {
                Auth0 = new Auth0Config
                {
                    Enabled = true,
                    Domain = TestAuth0Domain,
                    ClientId = TestClientId
                }
            };

            // Act
            var result = await provider.GetIdentityAsync(config);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("auth0|user456", result.ObjectId);
            Assert.Equal("user@example.com", result.UserPrincipalName);
            Assert.Equal(4, result.Groups.Count);
            Assert.Contains("admins", result.Groups);
            Assert.Contains("aria-admins", result.Groups);
            Assert.Equal(2, result.Roles.Count);
            Assert.Contains("admin", result.Roles);
            Assert.Contains("contributor", result.Roles);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", null);
        }
    }

    [Fact]
    public async Task GetIdentity_TokenWithScopeAsRoles_ParsesCorrectly()
    {
        // Arrange - Auth0 sometimes returns scopes as space-separated string in "scope" claim
        var token = CreateTestJwtWithClaims(new Dictionary<string, object>
        {
            ["sub"] = "user789",
            ["scope"] = "openid profile email read:assets write:assets"
        });
        Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", token);

        try
        {
            var provider = new Auth0IdentityProvider();
            var config = new AriaConfig
            {
                Auth0 = new Auth0Config
                {
                    Enabled = true,
                    Domain = TestAuth0Domain
                }
            };

            // Act
            var result = await provider.GetIdentityAsync(config);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("read:assets", result.Roles);
            Assert.Contains("write:assets", result.Roles);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", null);
        }
    }

    private static string CreateTestJwt() =>
        CreateTestJwtWithClaims(new Dictionary<string, object>
        {
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
            ["iss"] = $"https://{TestAuth0Domain}/",
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
