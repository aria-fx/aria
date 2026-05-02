// ─────────────────────────────────────────────────────────────
// Services/GovernanceService.cs
// Enforces OASF governance policies at install time.
// ─────────────────────────────────────────────────────────────

using Aria.Cli.Models;

namespace Aria.Cli.Services;

public sealed record GovernanceResult(
    bool Allowed,
    string? Reason = null,
    List<string>? RequiredApprovals = null);

public sealed class GovernanceService
{
    /// <summary>
    /// Validates that a consumer is allowed to install an asset
    /// based on sensitivity ceiling and consumer allow-list.
    /// </summary>
    public GovernanceResult ValidateInstall(
        AriaConfig config,
        OasfGovernanceOverlay governance)
    {
        var policy = governance.Governance;

        // Check sensitivity ceiling
        if (SensitivityTiers.Exceeds(policy.SensitivityTier, config.SensitivityCeiling))
        {
            return new GovernanceResult(
                Allowed: false,
                Reason: $"Asset sensitivity '{policy.SensitivityTier}' exceeds your ceiling " +
                        $"'{config.SensitivityCeiling}'. Request elevated access from the " +
                        $"AI Governance team.",
                RequiredApprovals: policy.ApprovalChain);
        }

        // Check consumer allow-list
        if (policy.AllowedConsumers.Count > 0 &&
            !policy.AllowedConsumers.Contains(config.ConsumerId, StringComparer.OrdinalIgnoreCase))
        {
            return new GovernanceResult(
                Allowed: false,
                Reason: $"Consumer '{config.ConsumerId}' is not in the allowed consumers " +
                        $"list for this asset. Allowed: [{string.Join(", ", policy.AllowedConsumers)}]",
                RequiredApprovals: policy.ApprovalChain);
        }

        return new GovernanceResult(Allowed: true);
    }

    /// <summary>
    /// Full audit: ceiling + consumer + dependency scan.
    /// </summary>
    public async Task<List<GovernanceResult>> AuditAsync(
        AriaConfig config,
        OasfRecord record,
        OasfGovernanceOverlay governance,
        OciRegistryService registry)
    {
        var results = new List<GovernanceResult>();

        // Self-check
        results.Add(ValidateInstall(config, governance));

        // Transitive dependency scan
        foreach (var module in record.Modules.Where(m => m.Ref != null))
        {
            var depGov = await registry.FetchGovernanceAsync(module.Ref!);
            if (depGov != null)
            {
                var depResult = ValidateInstall(config, depGov);
                if (!depResult.Allowed)
                {
                    results.Add(depResult with
                    {
                        Reason = $"Dependency '{module.Ref}': {depResult.Reason}"
                    });
                }
            }
        }

        return results;
    }
}
