using Aria.Auth.Core.Models;

namespace Aria.Auth.Core.Services;

public sealed record EffectiveAccessContext(
    string ConsumerId,
    string SensitivityCeiling,
    HashSet<string> PurviewRoles,
    ResolvedIdentity? Identity,
    List<string> MatchedRules);

public sealed class AccessPolicyService
{
    private readonly IdentityProviderFactory _identityProviderFactory;

    public AccessPolicyService(IdentityProviderFactory identityProviderFactory)
    {
        _identityProviderFactory = identityProviderFactory;
    }

    public async Task<EffectiveAccessContext> ResolveAsync(AriaConfig config)
    {
        var identityProvider = _identityProviderFactory.Resolve(config);
        var identity = await identityProvider.GetIdentityAsync(config);

        var consumerId = identity?.UserPrincipalName
            ?? identity?.ObjectId
            ?? config.ConsumerId;

        var ceiling = config.SensitivityCeiling;
        var purviewRoles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedRules = new List<string>();

        if (identity != null)
        {
            foreach (var rule in config.AccessRules)
            {
                if (!IsRuleMatch(rule, identity))
                    continue;

                matchedRules.Add(string.IsNullOrWhiteSpace(rule.Name) ? "unnamed" : rule.Name);

                if (!SensitivityTiers.Exceeds(rule.SensitivityCeiling, ceiling))
                    continue;

                ceiling = rule.SensitivityCeiling;

                foreach (var role in rule.PurviewRoles)
                {
                    if (!string.IsNullOrWhiteSpace(role))
                        purviewRoles.Add(role);
                }
            }
        }

        return new EffectiveAccessContext(consumerId, ceiling, purviewRoles, identity, matchedRules);
    }

    private static bool IsRuleMatch(AccessRule rule, ResolvedIdentity identity)
    {
        var hasGroupConstraint = rule.AnyEntraGroups.Count > 0;
        var hasRoleConstraint = rule.AnyEntraRoles.Count > 0;

        if (!hasGroupConstraint && !hasRoleConstraint)
            return false;

        var groupMatch = hasGroupConstraint && rule.AnyEntraGroups.Any(identity.Groups.Contains);
        var roleMatch = hasRoleConstraint && rule.AnyEntraRoles.Any(identity.Roles.Contains);

        return groupMatch || roleMatch;
    }
}
