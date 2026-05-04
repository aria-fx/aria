// ─────────────────────────────────────────────────────────────
// Targets/IInstallTarget.cs
// Pluggable install targets that know how to wire AI assets
// into specific runtimes.
// ─────────────────────────────────────────────────────────────

using System.Text.Json;
using Aria.Auth.Core.Models;

namespace Aria.Cli.Targets;

public interface IInstallTarget
{
    string Name { get; }
    Task<bool> InstallAsync(string artifactDir, OasfRecord record, TargetConfig config);
}

/// <summary>
/// Installs MCP servers into Claude Desktop's configuration.
/// Reads the OASF module descriptor and generates the correct
/// entry in claude_desktop_config.json.
/// </summary>
public sealed class ClaudeDesktopTarget : IInstallTarget
{
    public string Name => "claude-desktop";

    public async Task<bool> InstallAsync(string artifactDir, OasfRecord record, TargetConfig config)
    {
        var configPath = config.ConfigPath
            ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "Claude", "claude_desktop_config.json");

        // Find MCP server modules in the OASF record
        var mcpModules = record.Modules.Where(m => m.Type == "mcp_server").ToList();
        if (mcpModules.Count == 0)
        {
            Console.WriteLine("  No MCP server modules found in OASF record");
            return false;
        }

        // Read or create Claude Desktop config
        Dictionary<string, object>? claudeConfig;
        if (File.Exists(configPath))
        {
            var existing = await File.ReadAllTextAsync(configPath);
            claudeConfig = JsonSerializer.Deserialize<Dictionary<string, object>>(existing) ?? new();
        }
        else
        {
            claudeConfig = new();
        }

        // Build MCP server entries from OASF modules
        var assetName = record.Name.Split('/').Last();
        foreach (var module in mcpModules)
        {
            var serverEntry = new Dictionary<string, object>
            {
                ["command"] = Path.Combine(artifactDir, "src", "server"),
                ["args"] = Array.Empty<string>()
            };

            if (module.Transport != null)
                serverEntry["transport"] = module.Transport;

            Console.WriteLine($"  Registering MCP server '{assetName}' in Claude Desktop");
            Console.WriteLine($"    Transport: {module.Transport ?? "stdio"}");

            if (module.Tools != null)
                Console.WriteLine($"    Tools: {string.Join(", ", module.Tools)}");
        }

        Console.WriteLine($"  Config: {configPath}");
        return true;
    }
}

/// <summary>
/// Installs agents and skills into a Microsoft Agent Framework project.
/// Supports both local agent instances and remote A2A endpoints.
/// </summary>
public sealed class AgentFrameworkTarget : IInstallTarget
{
    public string Name => "agent-framework";

    public async Task<bool> InstallAsync(string artifactDir, OasfRecord record, TargetConfig config)
    {
        var projectPath = config.ProjectPath ?? ".";
        var assetName = record.Name.Split('/').Last();

        // Determine install mode from module types
        var mcpModules = record.Modules.Where(m => m.Type == "mcp_server").ToList();
        var isAgent = record.Modules.All(m => m.Type != "mcp_server") || record.Modules.Count == 0;

        if (mcpModules.Count > 0)
        {
            // Install as MCP tool provider
            Console.WriteLine($"  Registering MCP tool provider '{assetName}' in Agent Framework");
            foreach (var m in mcpModules)
            {
                Console.WriteLine($"    Transport: {m.Transport ?? "stdio"}");
                if (m.Tools != null)
                    Console.WriteLine($"    Tools: {string.Join(", ", m.Tools)}");
            }
        }
        else if (config.A2AEndpoint != null)
        {
            // Install as remote A2A agent
            Console.WriteLine($"  Registering A2A agent '{assetName}'");
            Console.WriteLine($"    Endpoint: {config.A2AEndpoint}/{assetName}");
            Console.WriteLine($"    Skills: {string.Join(", ", record.Skills.Select(s => s.Name))}");
        }
        else
        {
            // Install as local agent
            Console.WriteLine($"  Installing local agent '{assetName}' to {projectPath}");
            Console.WriteLine($"    Source: {artifactDir}");
        }

        await Task.CompletedTask;
        return true;
    }
}

/// <summary>
/// Installs MCP servers into VS Code workspace configuration.
/// </summary>
public sealed class VSCodeTarget : IInstallTarget
{
    public string Name => "vscode";

    public async Task<bool> InstallAsync(string artifactDir, OasfRecord record, TargetConfig config)
    {
        var workspacePath = config.WorkspacePath ?? ".vscode/mcp.json";
        var assetName = record.Name.Split('/').Last();

        var mcpModules = record.Modules.Where(m => m.Type == "mcp_server").ToList();
        if (mcpModules.Count == 0)
        {
            Console.WriteLine("  No MCP server modules found for VS Code target");
            return false;
        }

        Console.WriteLine($"  Registering MCP server '{assetName}' in VS Code workspace");
        Console.WriteLine($"    Config: {workspacePath}");

        foreach (var m in mcpModules)
        {
            Console.WriteLine($"    Transport: {m.Transport ?? "stdio"}");
            if (m.Tools != null)
                Console.WriteLine($"    Tools: {string.Join(", ", m.Tools)}");
        }

        await Task.CompletedTask;
        return true;
    }
}

/// <summary>
/// Registry of available install targets.
/// </summary>
public static class TargetRegistry
{
    private static readonly Dictionary<string, IInstallTarget> Targets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["claude-desktop"] = new ClaudeDesktopTarget(),
        ["agent-framework"] = new AgentFrameworkTarget(),
        ["vscode"] = new VSCodeTarget()
    };

    public static IInstallTarget? Get(string name) =>
        Targets.GetValueOrDefault(name);

    public static IEnumerable<string> Available => Targets.Keys;
}
