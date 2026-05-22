using Aria.Auth.Core.Models;
using Aria.Auth.Core.Services;
using Xunit;

namespace Aria.Auth.Core.Tests;

public sealed class RegistryPolicyEvaluatorTests
{
    [Fact]
    public void GovernedInternalSource_AllowsPolicyThenGovernanceValidation()
    {
        var config = new AriaConfig
        {
            ConsumerId = "hr-team",
            SensitivityCeiling = "confidential",
            RegistryPolicies = new(StringComparer.OrdinalIgnoreCase)
            {
                ["internal.registry/aria-assets"] = new()
                {
                    TrustTier = "internal_governed",
                    RequireGovernanceOverlay = true,
                    Priority = 100
                }
            }
        };

        var policy = RegistryPolicyEvaluator.ResolvePolicy(config, "internal.registry/aria-assets");
        var policyDecision = RegistryPolicyEvaluator.EvaluateInstallPolicy(
            policy,
            RegistryGovernanceStates.Governed,
            config.SensitivityCeiling);

        Assert.True(policyDecision.Allowed);

        var governance = new OasfGovernanceOverlay(new GovernancePolicy
        {
            SensitivityTier = "internal",
            AllowedConsumers = ["hr-team"]
        });
        var governanceDecision = new GovernanceService().ValidateInstall(config, governance);
        Assert.True(governanceDecision.Allowed);
    }

    [Fact]
    public void PublicSource_MissingOverlay_IsBlockedWhenOverlayIsRequired()
    {
        var policy = new RegistrySourcePolicyConfig
        {
            TrustTier = "public_curated",
            RequireGovernanceOverlay = true,
            Priority = 10
        };

        var decision = RegistryPolicyEvaluator.EvaluateInstallPolicy(
            policy,
            RegistryGovernanceStates.Ungoverned,
            accessCeiling: "restricted");

        Assert.False(decision.Allowed);
        Assert.Contains("requires governance overlay", decision.Reason);
    }

    [Fact]
    public void PublicSource_MissingOverlay_RequiresExplicitRelaxedPolicyAndSensitivityCap()
    {
        var strictDecision = RegistryPolicyEvaluator.EvaluateInstallPolicy(
            new RegistrySourcePolicyConfig
            {
                TrustTier = "public_untrusted",
                RequireGovernanceOverlay = true
            },
            RegistryGovernanceStates.Ungoverned,
            accessCeiling: "restricted");
        Assert.False(strictDecision.Allowed);

        var relaxedDecision = RegistryPolicyEvaluator.EvaluateInstallPolicy(
            new RegistrySourcePolicyConfig
            {
                TrustTier = "public_untrusted",
                RequireGovernanceOverlay = false,
                MaxSensitivityIfUngoverned = "internal"
            },
            RegistryGovernanceStates.Ungoverned,
            accessCeiling: "restricted");
        Assert.True(relaxedDecision.Allowed);
        Assert.Contains("max_sensitivity_if_ungoverned='internal'", relaxedDecision.Reason);
    }
}
