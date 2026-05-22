using System.Diagnostics;
using System.Text.Json;
using Aria.Auth.Core.Models;
using Aria.Cli.Services;
using Xunit;

namespace Aria.Cli.Tests;

public sealed class RegistryCommandsIntegrationTests : IDisposable
{
    private readonly string _sandboxRoot = Path.Combine(Path.GetTempPath(), "aria-registry-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AddListRemoveUpdateFlow_PersistsExpectedConfigChanges()
    {
        await WriteConfigAsync(new AriaConfig
        {
            Registries = ["ghcr.io/base/assets"],
            DefaultRegistry = "ghcr.io/base/assets"
        });

        Assert.Equal(0, (await RunAriaAsync("registry", "add", "ghcr.io/team/assets", "--name", "team")).ExitCode);
        Assert.Equal(0, (await RunAriaAsync("registry", "update", "team", "--url", "ghcr.io/team/new-assets")).ExitCode);
        Assert.Equal(0, (await RunAriaAsync("registry", "remove", "ghcr.io/base/assets")).ExitCode);

        var list = await RunAriaAsync("registry", "list", "--json");
        Assert.Equal(0, list.ExitCode);

        using var listJson = JsonDocument.Parse(list.StdOut);
        var item = Assert.Single(listJson.RootElement.EnumerateArray());
        Assert.Equal("team", item.GetProperty("name").GetString());
        Assert.Equal("ghcr.io/team/new-assets", item.GetProperty("url").GetString());
        Assert.True(item.GetProperty("isDefault").GetBoolean());

        var config = await ReadConfigAsync();
        Assert.Equal(["ghcr.io/team/new-assets"], config.Registries);
        Assert.Equal("ghcr.io/team/new-assets", config.DefaultRegistry);
        Assert.Equal("ghcr.io/team/new-assets", config.RegistryAliases["team"]);
    }

    [Fact]
    public async Task DuplicateAdd_IsRejectedWithClearError()
    {
        await WriteConfigAsync(new AriaConfig { Registries = [] });

        Assert.Equal(0, (await RunAriaAsync("registry", "add", "ghcr.io/team/assets")).ExitCode);
        var duplicate = await RunAriaAsync("registry", "add", "https://ghcr.io/team/assets/");

        Assert.Equal(1, duplicate.ExitCode);
        Assert.Contains("already configured", duplicate.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task InvalidUrl_IsRejected()
    {
        await WriteConfigAsync(new AriaConfig { Registries = [] });

        var invalid = await RunAriaAsync("registry", "add", "not-a-url");

        Assert.Equal(1, invalid.ExitCode);
        Assert.Contains("invalid", invalid.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SetDefault_UpdatesExpectedSource_AndListReflectsIt()
    {
        await WriteConfigAsync(new AriaConfig { Registries = [] });

        Assert.Equal(0, (await RunAriaAsync("registry", "add", "ghcr.io/team/primary", "--name", "primary")).ExitCode);
        Assert.Equal(0, (await RunAriaAsync("registry", "add", "ghcr.io/team/secondary", "--name", "secondary")).ExitCode);
        Assert.Equal(0, (await RunAriaAsync("registry", "set-default", "secondary")).ExitCode);

        var list = await RunAriaAsync("registry", "list", "--json");
        Assert.Equal(0, list.ExitCode);

        using var listJson = JsonDocument.Parse(list.StdOut);
        var entries = listJson.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.GetProperty("name").GetString() == "secondary" && e.GetProperty("isDefault").GetBoolean());
    }

    [Fact]
    public async Task Validate_ReportsReachableAndFailures_WithoutCrash()
    {
        await WriteConfigAsync(new AriaConfig { Registries = [] });

        var reachableRegistry = Path.Combine(_sandboxRoot, "reachable-registry");
        Directory.CreateDirectory(reachableRegistry);
        var unreachableRegistry = Path.Combine(_sandboxRoot, "missing-registry");

        Assert.Equal(0, (await RunAriaAsync("registry", "add", reachableRegistry, "--name", "ok")).ExitCode);
        Assert.Equal(0, (await RunAriaAsync("registry", "add", unreachableRegistry, "--name", "broken")).ExitCode);

        var validation = await RunAriaAsync("registry", "validate", "--json");

        Assert.Equal(1, validation.ExitCode);
        Assert.DoesNotContain("Unhandled exception", validation.StdErr, StringComparison.OrdinalIgnoreCase);

        using var json = JsonDocument.Parse(validation.StdOut);
        var entries = json.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.GetProperty("name").GetString() == "ok" && e.GetProperty("ok").GetBoolean());
        Assert.Contains(entries, e => e.GetProperty("name").GetString() == "broken" && !e.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task ExistingPreCommandConfig_LoadsAndRemainsUsable()
    {
        await WriteRawConfigAsync("""
            {
              "consumer_id": "legacy-team",
              "sensitivity_ceiling": "confidential",
              "registries": ["ghcr.io/legacy/assets"]
            }
            """);

        var list = await RunAriaAsync("registry", "list", "--json");

        Assert.Equal(0, list.ExitCode);
        using var listJson = JsonDocument.Parse(list.StdOut);
        var entry = Assert.Single(listJson.RootElement.EnumerateArray());
        Assert.Equal("ghcr.io/legacy/assets", entry.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Search_UsesRegistrySourcesManagedByRegistryCommands()
    {
        await WriteConfigAsync(new AriaConfig { Registries = [] });

        var registryRoot = Path.Combine(_sandboxRoot, "search-registry");
        var assetPath = Path.Combine(registryRoot, "asset-policy");
        Directory.CreateDirectory(assetPath);
        await File.WriteAllTextAsync(Path.Combine(assetPath, "oasf-record.json"), """
            {
              "schema_version": "1.0.0",
              "name": "aria.dev/skills/policy-lookup",
              "version": "1.0.0",
              "description": "policy search asset",
              "authors": ["ARIA Team <team@aria.dev>"],
              "skills": [{ "code": 30101, "name": "knowledge_retrieval/rag" }],
              "domains": [{ "name": "human_resources" }],
              "modules": [{ "type": "mcp_server", "ref": "oci://policy@sha256:abc", "transport": "stdio" }]
            }
            """);

        Assert.Equal(0, (await RunAriaAsync("registry", "add", registryRoot, "--name", "local", "--default")).ExitCode);
        var search = await RunAriaAsync("search", "--keyword", "policy");

        Assert.Equal(0, search.ExitCode);
        Assert.Contains("1 asset(s) found", search.StdOut, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("No assets found", search.StdOut, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (Directory.Exists(_sandboxRoot))
            Directory.Delete(_sandboxRoot, true);
    }

    private async Task WriteConfigAsync(AriaConfig config)
    {
        var configPath = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(configPath, json);
    }

    private async Task WriteRawConfigAsync(string json)
    {
        var configPath = GetConfigPath();
        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        await File.WriteAllTextAsync(configPath, json);
    }

    private async Task<AriaConfig> ReadConfigAsync()
    {
        var configPath = GetConfigPath();
        var json = await File.ReadAllTextAsync(configPath);
        return JsonSerializer.Deserialize<AriaConfig>(json)!;
    }

    private string GetConfigPath() => Path.Combine(_sandboxRoot, ".aria", "config.json");

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
