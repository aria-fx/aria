// ─────────────────────────────────────────────────────────────
// Services/OasfGovernanceService.cs
// Loads and validates the OASF Record and Governance Overlay
// at startup, providing runtime access to governance policy
// for the Purview middleware and sensitivity ceiling checks.
// ─────────────────────────────────────────────────────────────

using System.Text.Json;
using Microsoft.Extensions.Logging;
using Oasf.Sample.Agent.Models;

namespace Oasf.Sample.Agent.Services;

/// <summary>
/// Loads OASF Record and Governance Overlay from sidecar JSON files
/// and validates them at startup. Provides the governance policy
/// to middleware and tools at runtime.
/// </summary>
public sealed class OasfGovernanceService
{
    private readonly ILogger<OasfGovernanceService> _logger;

    public OasfRecord Record { get; private set; } = null!;
    public OasfGovernanceOverlay GovernanceOverlay { get; private set; } = null!;
    public GovernancePolicy Policy => GovernanceOverlay.Governance;

    public OasfGovernanceService(ILogger<OasfGovernanceService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Loads and validates both manifest files. Called at application startup.
    /// Throws if validation fails — the agent should not run ungoverned.
    /// </summary>
    public async Task InitializeAsync(
        string recordPath = "oasf-record.json",
        string governancePath = "oasf-governance.json")
    {
        _logger.LogInformation("Loading OASF Record from {Path}", recordPath);
        var recordJson = await File.ReadAllTextAsync(recordPath);
        Record = JsonSerializer.Deserialize<OasfRecord>(recordJson)
            ?? throw new InvalidOperationException("Failed to deserialize OASF Record");

        _logger.LogInformation("Loading Governance Overlay from {Path}", governancePath);
        var govJson = await File.ReadAllTextAsync(governancePath);
        GovernanceOverlay = JsonSerializer.Deserialize<OasfGovernanceOverlay>(govJson)
            ?? throw new InvalidOperationException("Failed to deserialize Governance Overlay");

        Validate();

        _logger.LogInformation(
            "OASF governance initialized: Asset={Name} v{Version}, " +
            "Sensitivity={Tier}, Ceiling={Ceiling}, Frameworks=[{Frameworks}]",
            Record.Name,
            Record.Version,
            Policy.SensitivityTier,
            Policy.DependencySensitivityCeiling,
            string.Join(", ", Policy.ComplianceFrameworks));
    }

    /// <summary>
    /// Validates the OASF Record and Governance Overlay at startup.
    /// Mirrors the checks performed by the oasf-validate GitHub Action.
    /// </summary>
    private void Validate()
    {
        // Required Record fields
        var requiredFields = new Dictionary<string, string?>
        {
            ["name"] = Record.Name,
            ["version"] = Record.Version,
            ["schema_version"] = Record.SchemaVersion,
        };

        foreach (var (field, value) in requiredFields)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException(
                    $"OASF Record validation failed: '{field}' is required");
        }

        if (Record.Skills.Count == 0)
            _logger.LogWarning("OASF Record has no skills declared — agent capabilities are undocumented");

        if (Record.Authors.Count == 0)
            throw new InvalidOperationException(
                "OASF Record validation failed: at least one author is required");

        // Governance overlay validation
        _ = SensitivityTiers.IndexOf(Policy.SensitivityTier); // throws if invalid
        _ = SensitivityTiers.IndexOf(Policy.DependencySensitivityCeiling);

        // Sensitivity ceiling check
        if (SensitivityTiers.Exceeds(Policy.SensitivityTier, Policy.DependencySensitivityCeiling))
        {
            throw new InvalidOperationException(
                $"Sensitivity ceiling violation: asset tier '{Policy.SensitivityTier}' " +
                $"exceeds declared ceiling '{Policy.DependencySensitivityCeiling}'");
        }

        _logger.LogInformation("OASF validation passed");
    }

    /// <summary>
    /// Checks whether a dependency's sensitivity tier is within
    /// this agent's declared ceiling. Used at runtime when the
    /// agent invokes external skills or knowledge bases.
    /// </summary>
    public bool IsDependencyAllowed(string dependencyTier)
    {
        var allowed = !SensitivityTiers.Exceeds(dependencyTier, Policy.DependencySensitivityCeiling);
        if (!allowed)
        {
            _logger.LogWarning(
                "Dependency with tier '{DependencyTier}' exceeds ceiling '{Ceiling}' — blocked",
                dependencyTier, Policy.DependencySensitivityCeiling);
        }
        return allowed;
    }
}
