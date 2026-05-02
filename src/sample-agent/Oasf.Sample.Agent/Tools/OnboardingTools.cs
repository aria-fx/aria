// ─────────────────────────────────────────────────────────────
// Tools/OnboardingTools.cs
// Agent tools representing the skills declared in oasf-record.json.
// Each tool maps to an OASF skill taxonomy entry and is subject
// to the governance overlay's sensitivity ceiling checks.
//
// These are registered with the Agent Framework via
// AIFunctionFactory and invoked automatically when the LLM
// determines they're needed to fulfill a user request.
// ─────────────────────────────────────────────────────────────

using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Oasf.Sample.Agent.Services;

namespace Oasf.Sample.Agent.Tools;

/// <summary>
/// Tools for the HR Onboarding Agent.
/// OASF Skills:
///   - nlp/nlu/intent_classification (id: 10101)
///   - nlp/nlg/text_completion (id: 10201)
///   - knowledge_retrieval/rag (id: 30101)
/// </summary>
public sealed class OnboardingTools
{
    private readonly OasfGovernanceService _governance;
    private readonly ILogger<OnboardingTools> _logger;

    // Simulated knowledge base with sensitivity classifications
    private static readonly Dictionary<string, (string Content, string SensitivityTier)> PolicyKnowledgeBase = new()
    {
        ["pto_policy"] = (
            "Employees accrue 15 days PTO per year, increasing to 20 days after 5 years of service. " +
            "PTO requests require manager approval 2 weeks in advance for periods exceeding 3 days.",
            "internal"
        ),
        ["benefits_overview"] = (
            "Full-time employees are eligible for medical, dental, and vision insurance starting on " +
            "the first day of the month following their start date. The company provides a 4% 401(k) match.",
            "internal"
        ),
        ["compensation_bands"] = (
            "Engineering compensation bands: L3 $95K-$130K, L4 $130K-$175K, L5 $175K-$230K, " +
            "L6 $230K-$310K. Bands are reviewed annually and adjusted for market conditions.",
            "confidential"
        ),
        ["employee_ssn_records"] = (
            "Employee SSN records are stored in the HR secure vault and are never exposed via agent interactions.",
            "restricted"
        ),
    };

    public OnboardingTools(OasfGovernanceService governance, ILogger<OnboardingTools> logger)
    {
        _governance = governance;
        _logger = logger;
    }

    /// <summary>
    /// Looks up an HR policy by topic. Enforces the OASF governance
    /// sensitivity ceiling before returning content.
    /// OASF Skill: knowledge_retrieval/rag (id: 30101)
    /// </summary>
    [Description("Look up an HR policy by topic name. Returns the policy content if the agent is authorized to access it based on sensitivity classification.")]
    public string LookupPolicy(
        [Description("The policy topic to look up (e.g., 'pto_policy', 'benefits_overview', 'compensation_bands')")] string topic)
    {
        _logger.LogInformation("Tool invoked: LookupPolicy(topic={Topic})", topic);

        if (!PolicyKnowledgeBase.TryGetValue(topic.ToLowerInvariant(), out var entry))
        {
            return $"No policy found for topic '{topic}'. Available topics: " +
                   string.Join(", ", PolicyKnowledgeBase.Keys);
        }

        // ── OASF sensitivity ceiling check ────────────────────
        // This is the runtime enforcement of the governance overlay's
        // dependency_sensitivity_ceiling. The agent can only access
        // knowledge that falls within its declared ceiling.
        if (!_governance.IsDependencyAllowed(entry.SensitivityTier))
        {
            _logger.LogWarning(
                "Policy '{Topic}' has sensitivity '{Tier}' which exceeds " +
                "agent ceiling '{Ceiling}' — access blocked",
                topic, entry.SensitivityTier, _governance.Policy.DependencySensitivityCeiling);

            return $"Access denied: the '{topic}' policy is classified as '{entry.SensitivityTier}' " +
                   $"which exceeds this agent's sensitivity ceiling of " +
                   $"'{_governance.Policy.DependencySensitivityCeiling}'. " +
                   $"Contact the AI Governance team to request an elevated access level.";
        }

        _logger.LogInformation(
            "Policy '{Topic}' (tier={Tier}) retrieved successfully — within ceiling '{Ceiling}'",
            topic, entry.SensitivityTier, _governance.Policy.DependencySensitivityCeiling);

        return entry.Content;
    }

    /// <summary>
    /// Generates a personalized onboarding welcome message.
    /// OASF Skill: nlp/nlg/text_completion (id: 10201)
    /// </summary>
    [Description("Generate a personalized onboarding welcome message for a new employee.")]
    public string GenerateWelcomeMessage(
        [Description("The new employee's first name")] string employeeName,
        [Description("The employee's department")] string department,
        [Description("The employee's start date (YYYY-MM-DD)")] string startDate)
    {
        _logger.LogInformation(
            "Tool invoked: GenerateWelcomeMessage(name={Name}, dept={Dept}, start={Start})",
            employeeName, department, startDate);

        return $"Welcome to the team, {employeeName}! We're excited to have you join the " +
               $"{department} department starting {startDate}. Your onboarding checklist has been " +
               $"prepared and your manager will reach out within the next 24 hours to schedule " +
               $"your first-week orientation. In the meantime, please review the benefits overview " +
               $"and PTO policy using this assistant.";
    }

    /// <summary>
    /// Lists the onboarding checklist items for a new employee.
    /// OASF Skill: nlp/nlu/intent_classification (id: 10101)
    /// </summary>
    [Description("Get the onboarding checklist for a new employee in a specific department.")]
    public string GetOnboardingChecklist(
        [Description("The employee's department")] string department)
    {
        _logger.LogInformation("Tool invoked: GetOnboardingChecklist(dept={Dept})", department);

        return $"""
            Onboarding Checklist for {department}:
            1. Complete I-9 employment verification (Day 1)
            2. Enroll in benefits (within 30 days of start date)
            3. Set up direct deposit
            4. Complete security awareness training
            5. Review and acknowledge employee handbook
            6. Meet with manager for 30-60-90 day goal setting
            7. Join department Slack/Teams channels
            8. Complete department-specific orientation modules
            """;
    }
}
