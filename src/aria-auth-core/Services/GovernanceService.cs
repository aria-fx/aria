using Aria.Auth.Core.Models;

namespace Aria.Auth.Core.Services;

public sealed record GovernanceResult(
    bool Allowed,
    string? Reason = null,
    List<string>? RequiredApprovals = null);

public interface IGovernanceOverlayResolver
{
    Task<OasfGovernanceOverlay?> FetchGovernanceAsync(string ociReference);
}

public sealed class GovernanceService
{
    public GovernanceResult ValidateInstall(
        AriaConfig config,
        OasfGovernanceOverlay governance,
        EffectiveAccessContext? access = null)
    {
        var policy = governance.Governance;
        var ceiling = access?.SensitivityCeiling ?? config.SensitivityCeiling;
        var consumerId = access?.ConsumerId ?? config.ConsumerId;

        if (SensitivityTiers.Exceeds(policy.SensitivityTier, ceiling))
        {
            return new GovernanceResult(
                Allowed: false,
                Reason: $"Asset sensitivity '{policy.SensitivityTier}' exceeds your ceiling " +
                        $"'{ceiling}'. Request elevated access from the " +
                        $"AI Governance team.",
                RequiredApprovals: policy.ApprovalChain);
        }

        if (policy.AllowedConsumers.Count > 0 &&
            !policy.AllowedConsumers.Contains(consumerId, StringComparer.OrdinalIgnoreCase))
        {
            return new GovernanceResult(
                Allowed: false,
                Reason: $"Consumer '{consumerId}' is not in the allowed consumers " +
                        $"list for this asset. Allowed: [{string.Join(", ", policy.AllowedConsumers)}]",
                RequiredApprovals: policy.ApprovalChain);
        }

        if (access?.Identity != null)
        {
            if (policy.AllowedEntraGroups.Count > 0 &&
                !policy.AllowedEntraGroups.Any(access.Identity.Groups.Contains))
            {
                return new GovernanceResult(
                    Allowed: false,
                    Reason: $"User is not in any allowed Entra groups for this asset. Allowed groups: " +
                            $"[{string.Join(", ", policy.AllowedEntraGroups)}]",
                    RequiredApprovals: policy.ApprovalChain);
            }

            if (policy.AllowedEntraRoles.Count > 0 &&
                !policy.AllowedEntraRoles.Any(access.Identity.Roles.Contains))
            {
                return new GovernanceResult(
                    Allowed: false,
                    Reason: $"User does not have required Entra role/scope for this asset. Allowed roles: " +
                            $"[{string.Join(", ", policy.AllowedEntraRoles)}]",
                    RequiredApprovals: policy.ApprovalChain);
            }
        }

        if (config.Purview?.RequiredRolesBySensitivity is { Count: > 0 }
            && config.Purview.RequiredRolesBySensitivity.TryGetValue(policy.SensitivityTier, out var requiredPurviewRoles)
            && requiredPurviewRoles.Count > 0)
        {
            var granted = access?.PurviewRoles ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var missing = requiredPurviewRoles.Where(r => !granted.Contains(r)).ToList();
            if (missing.Count > 0)
            {
                return new GovernanceResult(
                    Allowed: false,
                    Reason: $"Missing Purview role(s) for sensitivity '{policy.SensitivityTier}': " +
                            $"[{string.Join(", ", missing)}]",
                    RequiredApprovals: policy.ApprovalChain);
            }
        }

        return new GovernanceResult(Allowed: true);
    }

    public async Task<List<GovernanceResult>> AuditAsync(
        AriaConfig config,
        OasfRecord record,
        OasfGovernanceOverlay governance,
        IGovernanceOverlayResolver governanceResolver,
        EffectiveAccessContext? access = null)
    {
        var results = new List<GovernanceResult>
        {
            ValidateInstall(config, governance, access)
        };

        foreach (var module in record.Modules.Where(m => m.Ref != null))
        {
            var depGov = await governanceResolver.FetchGovernanceAsync(module.Ref!);
            if (depGov != null)
            {
                var depResult = ValidateInstall(config, depGov, access);
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
