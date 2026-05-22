using System.Text.Json.Serialization;

namespace Aria.Auth.Core.Models;

public sealed record AriaConfig
{
    [JsonPropertyName("consumer_id")]
    public string ConsumerId { get; init; } = "anonymous";

    [JsonPropertyName("sensitivity_ceiling")]
    public string SensitivityCeiling { get; init; } = "confidential";

    [JsonPropertyName("registries")]
    public List<string> Registries { get; init; } = ["ghcr.io"];

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName("registry_policies")]
    public Dictionary<string, RegistrySourcePolicyConfig> RegistryPolicies { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    [JsonPropertyName("registry_aliases")]
    public Dictionary<string, string> RegistryAliases { get; init; } = new(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("default_registry")]
    public string? DefaultRegistry { get; init; }

    [JsonPropertyName("targets")]
    public Dictionary<string, TargetConfig> Targets { get; init; } = new();

    [JsonPropertyName("purview")]
    public PurviewConfig? Purview { get; init; }

    [JsonPropertyName("entra")]
    public EntraConfig? Entra { get; init; }

    [JsonPropertyName("access_rules")]
    public List<AccessRule> AccessRules { get; init; } = [];

    [JsonPropertyName("auth")]
    public AuthConfig Auth { get; init; } = new();

    [JsonPropertyName("okta")]
    public OktaConfig? Okta { get; init; }

    [JsonPropertyName("auth0")]
    public Auth0Config? Auth0 { get; init; }
}

public sealed record TargetConfig
{
    [JsonPropertyName("config_path")]
    public string? ConfigPath { get; init; }

    [JsonPropertyName("project_path")]
    public string? ProjectPath { get; init; }

    [JsonPropertyName("a2a_endpoint")]
    public string? A2AEndpoint { get; init; }

    [JsonPropertyName("workspace_path")]
    public string? WorkspacePath { get; init; }
}

public sealed record PurviewConfig
{
    [JsonPropertyName("account")]
    public string Account { get; init; } = "";

    [JsonPropertyName("tenant_id")]
    public string TenantId { get; init; } = "";

    [JsonPropertyName("required_roles_by_sensitivity")]
    public Dictionary<string, List<string>> RequiredRolesBySensitivity { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed record EntraConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = false;

    [JsonPropertyName("tenant_id")]
    public string? TenantId { get; init; }

    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }

    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; init; } = ["https://management.azure.com/.default"];
}

public sealed record AuthConfig
{
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = "entra";

    [JsonPropertyName("enable_experimental_providers")]
    public bool EnableExperimentalProviders { get; init; } = false;
}

public sealed record OktaConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = false;

    [JsonPropertyName("issuer")]
    public string? Issuer { get; init; }

    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }

    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; init; } = [];

    [JsonPropertyName("access_token_env_var")]
    public string AccessTokenEnvVar { get; init; } = "OKTA_ACCESS_TOKEN";

    [JsonPropertyName("access_token_file")]
    public string? AccessTokenFile { get; init; }

    [JsonPropertyName("token_endpoint")]
    public string? TokenEndpoint { get; init; }

    [JsonPropertyName("client_secret_env_var")]
    public string ClientSecretEnvVar { get; init; } = "OKTA_CLIENT_SECRET";
}

public sealed record Auth0Config
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = false;

    [JsonPropertyName("domain")]
    public string? Domain { get; init; }

    [JsonPropertyName("client_id")]
    public string? ClientId { get; init; }

    [JsonPropertyName("audience")]
    public string? Audience { get; init; }

    [JsonPropertyName("scopes")]
    public List<string> Scopes { get; init; } = [];

    [JsonPropertyName("access_token_file")]
    public string? AccessTokenFile { get; init; }
}

public sealed record AccessRule
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("any_entra_groups")]
    public List<string> AnyEntraGroups { get; init; } = [];

    [JsonPropertyName("any_entra_roles")]
    public List<string> AnyEntraRoles { get; init; } = [];

    [JsonPropertyName("sensitivity_ceiling")]
    public string SensitivityCeiling { get; init; } = "confidential";

    [JsonPropertyName("purview_roles")]
    public List<string> PurviewRoles { get; init; } = [];
}
