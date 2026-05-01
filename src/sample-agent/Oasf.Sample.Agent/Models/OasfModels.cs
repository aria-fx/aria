// ─────────────────────────────────────────────────────────────
// Models/OasfModels.cs
// Strongly-typed representations of OASF Record and Governance
// Overlay manifests. These are deserialized from the sidecar
// JSON files and used throughout the application for runtime
// governance enforcement.
// ─────────────────────────────────────────────────────────────

using System.Text.Json.Serialization;

namespace Oasf.Sample.Agent.Models;

/// <summary>
/// OASF Record — the canonical metadata document for an AI asset.
/// Maps to the OASF Record schema (schema.oasf.outshift.com).
/// </summary>
public sealed record OasfRecord
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("schema_version")]
    public required string SchemaVersion { get; init; }

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

public sealed record OasfSkill
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

public sealed record OasfDomain
{
    [JsonPropertyName("name")]
    public required string Name { get; init; }
}

public sealed record OasfModule
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("ref")]
    public string? Ref { get; init; }

    [JsonPropertyName("transport")]
    public string? Transport { get; init; }

    [JsonPropertyName("tools")]
    public List<string>? Tools { get; init; }
}

public sealed record OasfLocator
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("urls")]
    public List<string> Urls { get; init; } = [];
}

/// <summary>
/// OASF Governance Overlay — enterprise-specific policy envelope
/// that extends the OASF Record with sensitivity classification,
/// approval chains, and compliance framework annotations.
/// </summary>
public sealed record OasfGovernanceOverlay
{
    [JsonPropertyName("governance")]
    public required GovernancePolicy Governance { get; init; }
}

public sealed record GovernancePolicy
{
    [JsonPropertyName("sensitivity_tier")]
    public required string SensitivityTier { get; init; }

    [JsonPropertyName("data_classifications")]
    public List<string> DataClassifications { get; init; } = [];

    [JsonPropertyName("purview_label_id")]
    public string PurviewLabelId { get; init; } = "";

    [JsonPropertyName("approval_chain")]
    public List<string> ApprovalChain { get; init; } = [];

    [JsonPropertyName("allowed_consumers")]
    public List<string> AllowedConsumers { get; init; } = [];

    [JsonPropertyName("max_data_retention_days")]
    public int MaxDataRetentionDays { get; init; } = 90;

    [JsonPropertyName("audit_level")]
    public required string AuditLevel { get; init; }

    [JsonPropertyName("dependency_sensitivity_ceiling")]
    public string DependencySensitivityCeiling { get; init; } = "restricted";

    [JsonPropertyName("compliance_frameworks")]
    public List<string> ComplianceFrameworks { get; init; } = [];
}

/// <summary>
/// Ordered sensitivity tiers for the inheritance model.
/// Index position determines hierarchy (higher index = more sensitive).
/// </summary>
public static class SensitivityTiers
{
    public static readonly string[] Ordered =
    [
        "public",
        "internal",
        "confidential",
        "highly_confidential",
        "restricted"
    ];

    public static int IndexOf(string tier) =>
        Array.IndexOf(Ordered, tier) is var idx && idx >= 0
            ? idx
            : throw new ArgumentException($"Unknown sensitivity tier: {tier}");

    public static bool Exceeds(string tier, string ceiling) =>
        IndexOf(tier) > IndexOf(ceiling);
}
