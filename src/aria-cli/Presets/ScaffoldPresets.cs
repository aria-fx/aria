// ─────────────────────────────────────────────────────────────
// ScaffoldPresets.cs
// Curated preset bundles installable via `aria scaffold --preset`.
// ─────────────────────────────────────────────────────────────

namespace Aria.Cli.Presets;

public sealed record ScaffoldPresetAsset(string Reference, string Kind);

public sealed record ScaffoldPreset(
    string Name,
    IReadOnlyList<string> Aliases,
    string Description,
    IReadOnlyList<ScaffoldPresetAsset> Assets);

public static class ScaffoldPresets
{
    public const string SkillKind = "skill";
    public const string AgentKind = "agent";

    public static IReadOnlyList<ScaffoldPreset> All { get; } =
    [
        new ScaffoldPreset(
            Name: "usage-eval",
            Aliases: ["provider-usage-evaluator"],
            Description: "Provider/cloud usage evaluation bundle (4 MCP skills + 1 agent)",
            Assets:
            [
                new("ghcr.io/aria-fx/aria-skills/aria.dev-skills-usage-ingest-normalize:1.0.1", SkillKind),
                new("ghcr.io/aria-fx/aria-skills/aria.dev-skills-usage-eval-metrics:1.0.1", SkillKind),
                new("ghcr.io/aria-fx/aria-skills/aria.dev-skills-usage-conformance:1.0.1", SkillKind),
                new("ghcr.io/aria-fx/aria-skills/aria.dev-skills-usage-reporting:1.0.1", SkillKind),
                new("ghcr.io/aria-fx/agents/provider-usage-evaluator:1.0.0", AgentKind)
            ])
    ];

    public static IReadOnlyList<string> AvailableNames { get; } =
        All.Select(p => p.Name).ToList();

    public static bool TryResolve(string nameOrAlias, out ScaffoldPreset? preset)
    {
        preset = null;
        if (string.IsNullOrWhiteSpace(nameOrAlias))
            return false;

        var candidate = nameOrAlias.Trim();
        preset = All.FirstOrDefault(p =>
            string.Equals(p.Name, candidate, StringComparison.OrdinalIgnoreCase) ||
            p.Aliases.Any(a => string.Equals(a, candidate, StringComparison.OrdinalIgnoreCase)));

        return preset != null;
    }

    public static string DescribeAvailable() =>
        string.Join(", ", All.Select(p =>
            p.Aliases.Count > 0
                ? $"{p.Name} (alias: {string.Join(", ", p.Aliases)})"
                : p.Name));
}
