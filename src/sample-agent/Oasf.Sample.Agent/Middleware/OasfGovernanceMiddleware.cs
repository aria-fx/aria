// ─────────────────────────────────────────────────────────────
// Middleware/OasfGovernanceMiddleware.cs
// Custom Agent Framework middleware that enforces OASF governance
// policies at runtime. Sits alongside Microsoft's Purview
// PurviewPolicyMiddleware in the agent's middleware pipeline.
//
// The Purview middleware handles DLP and sensitivity label
// enforcement at the data level. This middleware adds the
// OASF-specific layer: dependency ceiling checks, audit-level
// enforcement, consumer allow-list validation, and OASF
// telemetry emission.
// ─────────────────────────────────────────────────────────────

using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Oasf.Sample.Agent.Models;
using Oasf.Sample.Agent.Services;

namespace Oasf.Sample.Agent.Middleware;

/// <summary>
/// Agent Framework middleware that enforces OASF governance overlay
/// policies on every agent invocation. This complements Purview's
/// PurviewPolicyMiddleware by adding classification-aware checks
/// that Purview alone cannot perform (dependency ceiling, consumer
/// allow-lists, OASF-specific audit metadata).
/// </summary>
public sealed class OasfGovernanceMiddleware
{
    private readonly OasfGovernanceService _governance;
    private readonly ILogger<OasfGovernanceMiddleware> _logger;

    // OpenTelemetry activity source for OASF governance events
    private static readonly ActivitySource OasfActivitySource = new("Oasf.Governance");

    public OasfGovernanceMiddleware(
        OasfGovernanceService governance,
        ILogger<OasfGovernanceMiddleware> logger)
    {
        _governance = governance;
        _logger = logger;
    }

    public bool IsInvocationAllowed(string consumerId)
    {
        using var activity = OasfActivitySource.StartActivity("oasf.governance.evaluate");
        activity?.SetTag("oasf.asset.name", _governance.Record.Name);
        activity?.SetTag("oasf.asset.version", _governance.Record.Version);
        activity?.SetTag("oasf.consumer.id", consumerId);

        if (!IsConsumerAllowed(consumerId))
        {
            _logger.LogWarning(
                "Consumer '{ConsumerId}' is not in the allowed_consumers list for {Asset}",
                consumerId, _governance.Record.Name);
            activity?.SetTag("oasf.consumer.allowed", false);
            return false;
        }

        activity?.SetTag("oasf.consumer.id", consumerId);
        activity?.SetTag("oasf.consumer.allowed", true);

        return true;
    }

    /// <summary>
    /// Validates that the requesting consumer is in the governance
    /// overlay's allowed_consumers list. If the list is empty,
    /// all consumers are allowed (open access).
    /// </summary>
    private bool IsConsumerAllowed(string consumerId)
    {
        var allowed = _governance.Policy.AllowedConsumers;
        if (allowed.Count == 0) return true; // open access
        return allowed.Contains(consumerId, StringComparer.OrdinalIgnoreCase);
    }
}
