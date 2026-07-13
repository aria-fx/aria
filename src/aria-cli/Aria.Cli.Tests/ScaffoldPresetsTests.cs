using Aria.Cli.Presets;
using Xunit;

namespace Aria.Cli.Tests;

public sealed class ScaffoldPresetsTests
{
    [Fact]
    public void TryResolve_ByName_ReturnsUsageEvalPreset()
    {
        Assert.True(ScaffoldPresets.TryResolve("usage-eval", out var preset));
        Assert.NotNull(preset);
        Assert.Equal("usage-eval", preset!.Name);
    }

    [Fact]
    public void TryResolve_ByAlias_ReturnsUsageEvalPreset()
    {
        Assert.True(ScaffoldPresets.TryResolve("provider-usage-evaluator", out var preset));
        Assert.NotNull(preset);
        Assert.Equal("usage-eval", preset!.Name);
    }

    [Theory]
    [InlineData("USAGE-EVAL")]
    [InlineData("Usage-Eval")]
    [InlineData("  usage-eval  ")]
    [InlineData("PROVIDER-USAGE-EVALUATOR")]
    [InlineData(" provider-usage-evaluator ")]
    public void TryResolve_IsCaseInsensitiveAndTrims(string input)
    {
        Assert.True(ScaffoldPresets.TryResolve(input, out var preset));
        Assert.Equal("usage-eval", preset!.Name);
    }

    [Theory]
    [InlineData("bogus")]
    [InlineData("")]
    [InlineData("   ")]
    public void TryResolve_Unknown_ReturnsFalse(string input)
    {
        Assert.False(ScaffoldPresets.TryResolve(input, out var preset));
        Assert.Null(preset);
    }

    [Fact]
    public void UsageEvalPreset_HasExactPinnedReferences()
    {
        Assert.True(ScaffoldPresets.TryResolve("usage-eval", out var preset));

        var expected = new[]
        {
            "ghcr.io/aria-fx/aria-skills/aria.dev-skills-usage-ingest-normalize:1.0.1",
            "ghcr.io/aria-fx/aria-skills/aria.dev-skills-usage-eval-metrics:1.0.1",
            "ghcr.io/aria-fx/aria-skills/aria.dev-skills-usage-conformance:1.0.1",
            "ghcr.io/aria-fx/aria-skills/aria.dev-skills-usage-reporting:1.0.1",
            "ghcr.io/aria-fx/agents/provider-usage-evaluator:1.0.0"
        };

        Assert.Equal(expected, preset!.Assets.Select(a => a.Reference).ToArray());
    }

    [Fact]
    public void UsageEvalPreset_HasFourSkillsAndOneAgent()
    {
        Assert.True(ScaffoldPresets.TryResolve("usage-eval", out var preset));

        Assert.Equal(4, preset!.Assets.Count(a => a.Kind == ScaffoldPresets.SkillKind));
        var agent = Assert.Single(preset.Assets, a => a.Kind == ScaffoldPresets.AgentKind);
        Assert.Equal("ghcr.io/aria-fx/agents/provider-usage-evaluator:1.0.0", agent.Reference);
    }

    [Fact]
    public void AvailableNames_ContainsUsageEval()
    {
        Assert.Contains("usage-eval", ScaffoldPresets.AvailableNames);
    }

    [Fact]
    public void DescribeAvailable_IncludesNameAndAlias()
    {
        var described = ScaffoldPresets.DescribeAvailable();
        Assert.Contains("usage-eval", described);
        Assert.Contains("provider-usage-evaluator", described);
    }
}
