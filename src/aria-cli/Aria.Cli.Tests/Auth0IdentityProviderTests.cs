using System.Text;
using System.Text.Json;
using System.Net;
using System.Net.Http;
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

    [Fact]
    public async Task GetIdentity_DeviceFlow_AuthorizationPendingThenSuccess_ReturnsIdentity()
    {
        Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", null);
        var token = CreateTestJwt();

        var requests = new List<(string Url, string Body)>();
        var handler = new SequenceHttpMessageHandler(async (request, _) =>
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync();
            requests.Add((request.RequestUri?.ToString() ?? string.Empty, body));

            return requests.Count switch
            {
                1 => JsonResponse(HttpStatusCode.OK,
                    "{\"device_code\":\"device-code-123\",\"user_code\":\"ABCD-1234\",\"verification_uri\":\"https://example.auth0.com/activate\",\"expires_in\":10,\"interval\":0}"),
                2 => JsonResponse(HttpStatusCode.BadRequest, "{\"error\":\"authorization_pending\"}"),
                3 => JsonResponse(HttpStatusCode.OK, $"{{\"access_token\":\"{token}\"}}"),
                _ => throw new InvalidOperationException("Unexpected request sequence.")
            };
        });

        var provider = new Auth0IdentityProvider(new HttpClient(handler));
        var config = new AriaConfig
        {
            Auth0 = new Auth0Config
            {
                Enabled = true,
                Domain = TestAuth0Domain,
                ClientId = TestClientId
            }
        };

        var result = await provider.GetIdentityAsync(config);

        Assert.NotNull(result);
        Assert.Equal("user123", result.ObjectId);
        Assert.Equal(3, requests.Count);
        Assert.EndsWith("/oauth/device/code", requests[0].Url, StringComparison.Ordinal);
        Assert.EndsWith("/oauth/token", requests[1].Url, StringComparison.Ordinal);
        Assert.EndsWith("/oauth/token", requests[2].Url, StringComparison.Ordinal);
        Assert.Contains("client_id=test-client-id", requests[0].Body, StringComparison.Ordinal);
        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Adevice_code", requests[1].Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIdentity_DeviceFlow_DeviceCodeError_Throws()
    {
        Environment.SetEnvironmentVariable("AUTH0_ACCESS_TOKEN", null);
        var errorPayload = "{\"error\":\"invalid_request\",\"error_description\":\"client_id is required\"}";

        var handler = new SequenceHttpMessageHandler((request, _) =>
            Task.FromResult(JsonResponse(HttpStatusCode.BadRequest, errorPayload, "Bad Request")));

        var provider = new Auth0IdentityProvider(new HttpClient(handler));
        var config = new AriaConfig
        {
            Auth0 = new Auth0Config
            {
                Enabled = true,
                Domain = TestAuth0Domain,
                ClientId = TestClientId
            }
        };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetIdentityAsync(config));

        Assert.Contains("device code request failed", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Bad Request", ex.Message, StringComparison.Ordinal);
        Assert.Contains("client_id is required", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetIdentity_TokenFromTildeExpandedFile_ParsesIdentity()
    {
        // Arrange
        var token = CreateTestJwt();
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(homeDir))
            homeDir = Environment.GetEnvironmentVariable("HOME") ?? Path.GetTempPath();

        var testDir = Path.Combine(homeDir, ".aria-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testDir);
        var tokenFile = Path.Combine(testDir, "auth0-token.txt");
        await File.WriteAllTextAsync(tokenFile, token);

        try
        {
            var provider = new Auth0IdentityProvider();
            var relativePath = "~/" + Path.GetRelativePath(homeDir, tokenFile);
            var config = new AriaConfig
            {
                Auth0 = new Auth0Config
                {
                    Enabled = true,
                    Domain = TestAuth0Domain,
                    ClientId = TestClientId,
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

    [Fact]
    public async Task GetIdentity_TokenWithAudAsArray_ParsesTenantId()
    {
        // Arrange - Auth0 often returns aud as an array
        var token = CreateTestJwtWithClaims(new Dictionary<string, object>
        {
            ["sub"] = "user-with-array-aud",
            ["aud"] = new[] { "https://api.example.com", "https://api2.example.com" },
            ["email"] = "user@example.com"
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
            Assert.Equal("user-with-array-aud", result.ObjectId);
            Assert.Equal("https://api.example.com", result.TenantId); // First element of aud array
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

        // Create payload - only add defaults if not already in claims
        var payload = new Dictionary<string, object>(claims);
        if (!payload.ContainsKey("iss"))
            payload["iss"] = $"https://{TestAuth0Domain}/";
        if (!payload.ContainsKey("aud"))
            payload["aud"] = "test-audience";
        if (!payload.ContainsKey("iat"))
            payload["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (!payload.ContainsKey("exp"))
            payload["exp"] = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

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

    private static HttpResponseMessage JsonResponse(HttpStatusCode statusCode, string json, string? reasonPhrase = null)
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(reasonPhrase))
            response.ReasonPhrase = reasonPhrase;

        return response;
    }

    private sealed class SequenceHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return responseFactory(request, cancellationToken);
        }
    }
}
