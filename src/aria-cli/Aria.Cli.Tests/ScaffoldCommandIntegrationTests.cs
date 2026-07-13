using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Aria.Auth.Core.Models;
using Aria.Cli.Services;
using Xunit;

namespace Aria.Cli.Tests;

public sealed class ScaffoldCommandIntegrationTests : IDisposable
{
    private readonly string _sandboxRoot = Path.Combine(Path.GetTempPath(), "aria-scaffold-tests-" + Guid.NewGuid().ToString("N"));

    private static readonly (string DirName, string RecordName, string Version)[] PresetAssets =
    [
        ("aria.dev-skills-usage-ingest-normalize", "aria.dev/skills/usage-ingest-normalize", "1.0.1"),
        ("aria.dev-skills-usage-eval-metrics", "aria.dev/skills/usage-eval-metrics", "1.0.1"),
        ("aria.dev-skills-usage-conformance", "aria.dev/skills/usage-conformance", "1.0.1"),
        ("aria.dev-skills-usage-reporting", "aria.dev/skills/usage-reporting", "1.0.1"),
        ("provider-usage-evaluator", "aria.dev/agents/provider-usage-evaluator", "1.0.0")
    ];

    [Fact]
    public async Task Scaffold_FullBundle_InstallsAllFiveAssets()
    {
        await SeedPresetFixturesAsync();
        await WriteScaffoldConfigAsync();

        var result = await RunAriaAsync("scaffold", "--preset", "usage-eval", "--target", "claude-desktop");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Scaffold complete: 5/5 assets installed", Flatten(result.StdOut));

        foreach (var (_, recordName, _) in PresetAssets)
            Assert.True(Directory.Exists(GetCachePath(recordName)), $"expected cache dir for {recordName}");
    }

    [Fact]
    public async Task Scaffold_ReRun_IsIdempotent()
    {
        await SeedPresetFixturesAsync();
        await WriteScaffoldConfigAsync();

        var first = await RunAriaAsync("scaffold", "--preset", "usage-eval", "--target", "claude-desktop");
        var second = await RunAriaAsync("scaffold", "--preset", "usage-eval", "--target", "claude-desktop");

        Assert.Equal(0, first.ExitCode);
        Assert.Equal(0, second.ExitCode);
        Assert.Contains("Scaffold complete: 5/5 assets installed", Flatten(second.StdOut));

        foreach (var (_, recordName, _) in PresetAssets)
            Assert.True(Directory.Exists(GetCachePath(recordName)), $"expected cache dir for {recordName}");
    }

    [Fact]
    public async Task Scaffold_GovernanceBlocked_ContinuesThroughAllAssetsAndFails()
    {
        await SeedPresetFixturesAsync();
        await WriteScaffoldConfigAsync(sensitivityCeiling: "internal");

        var result = await RunAriaAsync("scaffold", "--preset", "usage-eval", "--target", "claude-desktop");
        var flat = Flatten(result.StdOut);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("exceeds your ceiling", flat);
        Assert.Contains("[5/5]", flat);
        Assert.Contains("Scaffold finished with failures: 0/5 assets installed", flat);
    }

    [Fact]
    public async Task Scaffold_UnknownPreset_FailsAndListsAvailablePresets()
    {
        await WriteScaffoldConfigAsync();

        var result = await RunAriaAsync("scaffold", "--preset", "bogus", "--target", "claude-desktop");
        var flat = Flatten(result.StdOut);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown preset 'bogus'", flat);
        Assert.Contains("usage-eval", flat);
        Assert.Contains("provider-usage-evaluator", flat);
    }

    [Fact]
    public async Task Scaffold_ByAlias_InstallsAllFiveAssets()
    {
        await SeedPresetFixturesAsync();
        await WriteScaffoldConfigAsync();

        var result = await RunAriaAsync("scaffold", "--preset", "provider-usage-evaluator", "--target", "claude-desktop");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Scaffold complete: 5/5 assets installed", Flatten(result.StdOut));
    }

    [Fact]
    public async Task Scaffold_SkillsOnly_SkipsAgent()
    {
        await SeedPresetFixturesAsync();
        await WriteScaffoldConfigAsync();

        var result = await RunAriaAsync("scaffold", "--preset", "usage-eval", "--target", "claude-desktop", "--skills-only");
        var flat = Flatten(result.StdOut);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("skipping 1 agent asset(s)", flat);
        Assert.Contains("Scaffold complete: 4/4 assets installed", flat);
        Assert.False(Directory.Exists(GetCachePath("aria.dev/agents/provider-usage-evaluator")));
    }

    [Fact]
    public async Task Scaffold_UnfetchableAsset_ReportsFailureButInstallsRest()
    {
        await SeedPresetFixturesAsync();
        Directory.Delete(Path.Combine(_sandboxRoot, "demo-assets", "aria.dev-skills-usage-reporting"), true);
        await WriteScaffoldConfigAsync();

        var result = await RunAriaAsync("scaffold", "--preset", "usage-eval", "--target", "claude-desktop");
        var flat = Flatten(result.StdOut);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Could not fetch OASF Record", flat);
        Assert.Contains("Scaffold finished with failures: 4/5 assets installed", flat);
    }

    [Fact]
    public async Task Scaffold_MissingPresetOption_IsParseError()
    {
        await WriteScaffoldConfigAsync();

        var result = await RunAriaAsync("scaffold", "--target", "claude-desktop");

        Assert.NotEqual(0, result.ExitCode);
    }

    [Fact]
    public async Task Scaffold_UnknownTarget_FailsBeforeInstallingAnything()
    {
        await SeedPresetFixturesAsync();
        await WriteScaffoldConfigAsync();

        var result = await RunAriaAsync("scaffold", "--preset", "usage-eval", "--target", "bogus-target");
        var flat = Flatten(result.StdOut);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Unknown target 'bogus-target'", flat);
        Assert.False(Directory.Exists(Path.Combine(_sandboxRoot, ".aria", "cache")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_sandboxRoot))
            Directory.Delete(_sandboxRoot, true);
    }

    private async Task SeedPresetFixturesAsync()
    {
        foreach (var (dirName, recordName, version) in PresetAssets)
        {
            var assetDir = Path.Combine(_sandboxRoot, "demo-assets", dirName);
            Directory.CreateDirectory(assetDir);

            await File.WriteAllTextAsync(Path.Combine(assetDir, "oasf-record.json"), $$"""
                {
                  "schema_version": "1.0.0",
                  "name": "{{recordName}}",
                  "version": "{{version}}",
                  "description": "Usage evaluation bundle fixture asset",
                  "authors": ["ARIA Team <team@aria.dev>"],
                  "skills": [{ "code": 30101, "name": "knowledge_retrieval/rag" }],
                  "domains": [{ "name": "platform/aria/finops" }],
                  "modules": [{ "type": "mcp_server", "ref": "oci://{{dirName}}@sha256:abc", "transport": "stdio" }]
                }
                """);

            await File.WriteAllTextAsync(Path.Combine(assetDir, "oasf-governance.json"), """
                {
                  "governance": {
                    "sensitivity_tier": "confidential",
                    "audit_level": "full",
                    "approval_chain": ["ai-governance"],
                    "allowed_consumers": ["all-employees"]
                  }
                }
                """);
        }
    }

    private async Task WriteScaffoldConfigAsync(string sensitivityCeiling = "confidential")
    {
        var config = new AriaConfig
        {
            ConsumerId = "all-employees",
            SensitivityCeiling = sensitivityCeiling,
            Registries = ["ghcr.io/aria-fx/aria-skills", "ghcr.io/aria-fx/agents"],
            RegistryPolicies = new Dictionary<string, RegistrySourcePolicyConfig>(StringComparer.OrdinalIgnoreCase)
            {
                ["ghcr.io/aria-fx/aria-skills"] = new() { TrustTier = "internal_governed", RequireGovernanceOverlay = true },
                ["ghcr.io/aria-fx/agents"] = new() { TrustTier = "internal_governed", RequireGovernanceOverlay = true }
            }
        };

        var configPath = Path.Combine(_sandboxRoot, ".aria", "config.json");
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configPath, json);
    }

    private string GetCachePath(string recordName) =>
        Path.Combine(_sandboxRoot, ".aria", "cache", recordName.Replace('/', '-'));

    // Spectre.Console wraps output at the console width, so multi-word phrases can be
    // split across lines; collapsing whitespace makes substring assertions wrap-safe.
    private static string Flatten(string text) => Regex.Replace(text, @"\s+", " ");

    private async Task<CliResult> RunAriaAsync(params string[] args)
    {
        var ariaDllPath = typeof(OciRegistryService).Assembly.Location;
        var process = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = _sandboxRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        process.StartInfo.ArgumentList.Add(ariaDllPath);
        foreach (var arg in args)
            process.StartInfo.ArgumentList.Add(arg);

        process.StartInfo.Environment["HOME"] = _sandboxRoot;
        process.StartInfo.Environment["USERPROFILE"] = _sandboxRoot;
        process.StartInfo.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";

        process.Start();
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        return new CliResult(
            process.ExitCode,
            (await stdOutTask).Trim(),
            (await stdErrTask).Trim());
    }

    private sealed record CliResult(int ExitCode, string StdOut, string StdErr);
}
