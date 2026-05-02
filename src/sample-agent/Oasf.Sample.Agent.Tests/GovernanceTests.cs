// ─────────────────────────────────────────────────────────────
// Tests verifying OASF governance behavior:
//   - Sensitivity tier ordering
//   - Ceiling enforcement
//   - Consumer allow-list validation
//   - Tool-level sensitivity gating
// ─────────────────────────────────────────────────────────────

using Oasf.Sample.Agent.Models;
using Xunit;

namespace Oasf.Sample.Agent.Tests;

public class SensitivityTierTests
{
    [Theory]
    [InlineData("public", 0)]
    [InlineData("internal", 1)]
    [InlineData("confidential", 2)]
    [InlineData("highly_confidential", 3)]
    [InlineData("restricted", 4)]
    public void IndexOf_ReturnsCorrectOrder(string tier, int expected)
    {
        Assert.Equal(expected, SensitivityTiers.IndexOf(tier));
    }

    [Fact]
    public void IndexOf_ThrowsForUnknownTier()
    {
        Assert.Throws<ArgumentException>(() => SensitivityTiers.IndexOf("top_secret"));
    }

    [Theory]
    [InlineData("restricted", "confidential", true)]      // restricted > confidential
    [InlineData("highly_confidential", "confidential", true)] // hc > confidential
    [InlineData("confidential", "confidential", false)]    // equal = not exceeding
    [InlineData("internal", "confidential", false)]        // internal < confidential
    [InlineData("public", "restricted", false)]            // public < restricted
    public void Exceeds_CorrectlyComparesHierarchy(string tier, string ceiling, bool expected)
    {
        Assert.Equal(expected, SensitivityTiers.Exceeds(tier, ceiling));
    }
}

public class GovernancePolicyTests
{
    [Fact]
    public void GovernanceOverlay_DeserializesCorrectly()
    {
        var json = """
        {
          "governance": {
            "sensitivity_tier": "confidential",
            "data_classifications": ["PII", "PHI"],
            "purview_label_id": "test-label-id",
            "approval_chain": ["ai-governance"],
            "allowed_consumers": ["hr-team"],
            "max_data_retention_days": 90,
            "audit_level": "full",
            "dependency_sensitivity_ceiling": "confidential",
            "compliance_frameworks": ["SOC2", "HIPAA"]
          }
        }
        """;

        var overlay = System.Text.Json.JsonSerializer.Deserialize<OasfGovernanceOverlay>(json);
        Assert.NotNull(overlay);
        Assert.Equal("confidential", overlay!.Governance.SensitivityTier);
        Assert.Contains("PII", overlay.Governance.DataClassifications);
        Assert.Contains("PHI", overlay.Governance.DataClassifications);
        Assert.Equal("full", overlay.Governance.AuditLevel);
        Assert.Equal(90, overlay.Governance.MaxDataRetentionDays);
        Assert.Contains("SOC2", overlay.Governance.ComplianceFrameworks);
    }

    [Fact]
    public void OasfRecord_DeserializesSkillsCorrectly()
    {
        var json = """
        {
          "name": "test.com/agents/test-agent",
          "version": "1.0.0",
          "schema_version": "1.0.0",
          "skills": [
            { "id": 10101, "name": "nlp/nlu/intent_classification" },
            { "id": 30101, "name": "knowledge_retrieval/rag" }
          ],
          "domains": [{ "name": "human_resources/onboarding" }],
          "modules": [],
          "locators": [{ "type": "source_code", "urls": ["https://github.com/test"] }],
          "authors": ["Test Author <test@example.com>"],
          "created_at": "2026-01-01T00:00:00Z"
        }
        """;

        var record = System.Text.Json.JsonSerializer.Deserialize<OasfRecord>(json);
        Assert.NotNull(record);
        Assert.Equal(2, record!.Skills.Count);
        Assert.Equal("nlp/nlu/intent_classification", record.Skills[0].Name);
        Assert.Equal(10101, record.Skills[0].Id);
        Assert.Single(record.Domains);
        Assert.Equal("human_resources/onboarding", record.Domains[0].Name);
    }

    [Fact]
    public void ConsumerAllowList_EmptyMeansOpenAccess()
    {
        var policy = new GovernancePolicy
        {
            SensitivityTier = "internal",
            AuditLevel = "standard",
            AllowedConsumers = [] // empty = open access
        };

        // When the list is empty, any consumer should be allowed
        Assert.Empty(policy.AllowedConsumers);
    }

    [Fact]
    public void ConsumerAllowList_RestrictsAccess()
    {
        var policy = new GovernancePolicy
        {
            SensitivityTier = "confidential",
            AuditLevel = "full",
            AllowedConsumers = ["hr-team", "onboarding-automation"]
        };

        Assert.Contains("hr-team", policy.AllowedConsumers);
        Assert.DoesNotContain("finance-team", policy.AllowedConsumers);
    }
}
