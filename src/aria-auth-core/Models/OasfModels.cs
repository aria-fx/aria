using System.Text.Json.Serialization;

namespace Aria.Auth.Core.Models;

public sealed record OasfRecord
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } = "";

    [JsonPropertyName("description")]
    public string Description { get; init; } = "";

    [JsonPropertyName("skills")]
    public List<OasfSkill> Skills { get; init; } = [];

    [JsonPropertyName("domains")]
    public List<OasfDomain> Domains { get; init; } = [];

    [JsonPropertyName("modules")]
    public List<OasfModule> Modules { get; init; } = [];

    [JsonPropertyName("locators")]
    public List<OasfLocator> Locators { get; init; } = [];

    [JsonPropertyName("authors")]
    public List<string> Authors { get; init; } = [];

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = "";
}

public sealed record OasfSkill(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("name")] string Name);

public sealed record OasfDomain(
    [property: JsonPropertyName("name")] string Name);

public sealed record OasfModule
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("ref")]
    public string? Ref { get; init; }

    [JsonPropertyName("transport")]
    public string? Transport { get; init; }

    [JsonPropertyName("tools")]
    public List<string>? Tools { get; init; }
}

public sealed record OasfLocator(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("urls")] List<string> Urls);

public sealed record OasfGovernanceOverlay(
    [property: JsonPropertyName("governance")] GovernancePolicy Governance);

public sealed record GovernancePolicy
{
    [JsonPropertyName("sensitivity_tier")]
    public string SensitivityTier { get; init; } = "internal";

    [JsonPropertyName("data_classifications")]
    public List<string> DataClassifications { get; init; } = [];

    [JsonPropertyName("approval_chain")]
    public List<string> ApprovalChain { get; init; } = [];

    [JsonPropertyName("allowed_consumers")]
    public List<string> AllowedConsumers { get; init; } = [];

    [JsonPropertyName("allowed_entra_groups")]
    public List<string> AllowedEntraGroups { get; init; } = [];

    [JsonPropertyName("allowed_entra_roles")]
    public List<string> AllowedEntraRoles { get; init; } = [];

    [JsonPropertyName("max_data_retention_days")]
    public int MaxDataRetentionDays { get; init; } = 90;

    [JsonPropertyName("audit_level")]
    public string AuditLevel { get; init; } = "standard";

    [JsonPropertyName("dependency_sensitivity_ceiling")]
    public string DependencySensitivityCeiling { get; init; } = "restricted";

    [JsonPropertyName("compliance_frameworks")]
    public List<string> ComplianceFrameworks { get; init; } = [];
}

public static class SensitivityTiers
{
    public static readonly string[] Ordered =
        ["public", "internal", "confidential", "highly_confidential", "restricted"];

    public static int IndexOf(string tier) =>
        Array.IndexOf(Ordered, tier) is var idx && idx >= 0
            ? idx
            : throw new ArgumentException($"Unknown sensitivity tier: {tier}");

    public static bool Exceeds(string tier, string ceiling) =>
        IndexOf(tier) > IndexOf(ceiling);
}

public sealed record InstalledAsset
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("version")]
    public string Version { get; init; } = "";

    [JsonPropertyName("module_type")]
    public string ModuleType { get; init; } = "";

    [JsonPropertyName("target")]
    public string Target { get; init; } = "";

    [JsonPropertyName("sensitivity_tier")]
    public string SensitivityTier { get; init; } = "";

    [JsonPropertyName("installed_at")]
    public DateTimeOffset InstalledAt { get; init; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("oci_reference")]
    public string OciReference { get; init; } = "";
}
