using System.Text.Json.Serialization;

namespace Aria.Auth.Core.Models;

public static class RegistryGovernanceStates
{
    public const string Governed = "governed";
    public const string Ungoverned = "ungoverned";
    public const string GovernanceUnreachable = "governance_unreachable";
}

public sealed record RegistrySourcePolicyConfig
{
    [JsonPropertyName("trust_tier")]
    public string TrustTier { get; init; } = "public_untrusted";

    [JsonPropertyName("require_governance_overlay")]
    public bool RequireGovernanceOverlay { get; init; } = false;

    [JsonPropertyName("max_sensitivity_if_ungoverned")]
    public string? MaxSensitivityIfUngoverned { get; init; }

    [JsonPropertyName("priority")]
    public int Priority { get; init; } = 0;
}

public sealed record RegistryPolicyDecision(
    bool Allowed,
    string Reason,
    string GovernanceState);

public static class RegistryPolicyEvaluator
{
    private static readonly RegistrySourcePolicyConfig DefaultPolicy = new();

    public static string? ResolveRegistryFromReference(string reference, IEnumerable<string> registries)
    {
        return registries
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .OrderByDescending(r => r.Length)
            .FirstOrDefault(r => reference.StartsWith($"{r.TrimEnd('/')}/", StringComparison.OrdinalIgnoreCase));
    }

    public static RegistrySourcePolicyConfig ResolvePolicy(AriaConfig config, string registry)
    {
        return config.RegistryPolicies.TryGetValue(registry, out var configured)
            ? configured
            : DefaultPolicy;
    }

    public static RegistryPolicyDecision EvaluateInstallPolicy(
        RegistrySourcePolicyConfig policy,
        string governanceState,
        string accessCeiling)
    {
        if (policy.RequireGovernanceOverlay && governanceState != RegistryGovernanceStates.Governed)
        {
            return new RegistryPolicyDecision(
                Allowed: false,
                Reason: $"Blocked by registry policy: trust_tier='{policy.TrustTier}' requires governance overlay, but state is '{governanceState}'.",
                GovernanceState: governanceState);
        }

        if (governanceState == RegistryGovernanceStates.Governed)
        {
            return new RegistryPolicyDecision(
                Allowed: true,
                Reason: $"Allowed by registry policy: governed source under trust_tier='{policy.TrustTier}'.",
                GovernanceState: governanceState);
        }

        if (!string.IsNullOrWhiteSpace(policy.MaxSensitivityIfUngoverned))
        {
            if (SensitivityTiers.Exceeds(policy.MaxSensitivityIfUngoverned!, accessCeiling))
            {
                return new RegistryPolicyDecision(
                    Allowed: false,
                    Reason: $"Blocked by registry policy: ungoverned source capped at '{policy.MaxSensitivityIfUngoverned}', which exceeds your ceiling '{accessCeiling}'.",
                    GovernanceState: governanceState);
            }

            return new RegistryPolicyDecision(
                Allowed: true,
                Reason: $"Allowed by relaxed policy: ungoverned source with max_sensitivity_if_ungoverned='{policy.MaxSensitivityIfUngoverned}'.",
                GovernanceState: governanceState);
        }

        return new RegistryPolicyDecision(
            Allowed: true,
            Reason: $"Allowed by relaxed policy: governance state '{governanceState}' accepted for trust_tier='{policy.TrustTier}'.",
            GovernanceState: governanceState);
    }
}
