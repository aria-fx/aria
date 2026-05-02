// ─────────────────────────────────────────────────────────────
// Program.cs
// ARIA Package Manager (aria) — CLI for discovering, validating,
// and installing OASF-governed AI assets from OCI registries.
// ─────────────────────────────────────────────────────────────

using System.CommandLine;
using System.Text.Json;
using Aria.Cli.Models;
using Aria.Cli.Services;
using Aria.Cli.Targets;
using Spectre.Console;

var configPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".aria", "config.json");

AriaConfig LoadConfig()
{
    if (File.Exists(configPath))
        return JsonSerializer.Deserialize<AriaConfig>(File.ReadAllText(configPath)) ?? new();
    return new();
}

var registry = new OciRegistryService();
var governance = new GovernanceService();

// ═══════════════════════════════════════════════════════════
// ROOT COMMAND
// ═══════════════════════════════════════════════════════════

var rootCommand = new RootCommand("aria — ARIA Package Manager for OASF-governed AI assets")
{
    Name = "aria"
};

// ═══════════════════════════════════════════════════════════
// SEARCH
// ═══════════════════════════════════════════════════════════

var searchCommand = new Command("search", "Discover AI assets by OASF skill, domain, or keyword");
var skillOption = new Option<string?>("--skill", "OASF skill taxonomy filter (e.g. knowledge_retrieval/rag)");
var domainOption = new Option<string?>("--domain", "OASF domain filter (e.g. human_resources)");
var keywordOption = new Option<string?>("--keyword", "Free-text keyword search");
searchCommand.AddOption(skillOption);
searchCommand.AddOption(domainOption);
searchCommand.AddOption(keywordOption);

searchCommand.SetHandler(async (string? skill, string? domain, string? keyword) =>
{
    var config = LoadConfig();
    var results = await registry.SearchAsync(skill, domain, keyword, config.Registries);

    if (results.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No assets found matching your criteria.[/]");
        return;
    }

    var table = new Table()
        .AddColumn("Name")
        .AddColumn("Version")
        .AddColumn("Type")
        .AddColumn("Skills")
        .AddColumn("Description")
        .Border(TableBorder.Rounded);

    foreach (var r in results)
    {
        var moduleType = r.Modules.FirstOrDefault()?.Type ?? "agent";
        var skills = string.Join(", ", r.Skills.Select(s => s.Name.Split('/').Last()));
        table.AddRow(
            $"[bold]{r.Name}[/]",
            r.Version,
            $"[cyan]{moduleType}[/]",
            skills,
            r.Description.Length > 50 ? r.Description[..50] + "..." : r.Description);
    }

    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine($"\n[dim]{results.Count} asset(s) found[/]");
}, skillOption, domainOption, keywordOption);

rootCommand.AddCommand(searchCommand);

// ═══════════════════════════════════════════════════════════
// INSPECT
// ═══════════════════════════════════════════════════════════

var inspectCommand = new Command("inspect", "Display OASF Record and governance overlay for an asset");
var inspectRefArg = new Argument<string>("reference", "OCI artifact reference (e.g. ghcr.io/org/asset:tag)");
inspectCommand.AddArgument(inspectRefArg);

inspectCommand.SetHandler(async (string reference) =>
{
    AnsiConsole.MarkupLine($"[dim]Fetching OASF metadata from {reference}...[/]\n");

    var record = await registry.FetchRecordAsync(reference);
    if (record == null)
    {
        AnsiConsole.MarkupLine("[red]Could not fetch OASF Record from the specified reference.[/]");
        return;
    }

    var gov = await registry.FetchGovernanceAsync(reference);

    // Display OASF Record
    var panel = new Panel(new Rows(
        new Markup($"[bold]Name:[/] {record.Name}"),
        new Markup($"[bold]Version:[/] {record.Version}"),
        new Markup($"[bold]Schema:[/] {record.SchemaVersion}"),
        new Markup($"[bold]Description:[/] {record.Description}"),
        new Markup($"[bold]Authors:[/] {string.Join(", ", record.Authors)}"),
        new Markup($"[bold]Skills:[/] {string.Join(", ", record.Skills.Select(s => s.Name))}"),
        new Markup($"[bold]Domains:[/] {string.Join(", ", record.Domains.Select(d => d.Name))}"),
        new Markup($"[bold]Modules:[/] {string.Join(", ", record.Modules.Select(m => m.Type))}")
    )).Header("[cyan]OASF Record[/]").Border(BoxBorder.Rounded);

    AnsiConsole.Write(panel);

    if (gov != null)
    {
        var g = gov.Governance;
        var govPanel = new Panel(new Rows(
            new Markup($"[bold]Sensitivity:[/] {g.SensitivityTier}"),
            new Markup($"[bold]Classifications:[/] {string.Join(", ", g.DataClassifications)}"),
            new Markup($"[bold]Ceiling:[/] {g.DependencySensitivityCeiling}"),
            new Markup($"[bold]Audit level:[/] {g.AuditLevel}"),
            new Markup($"[bold]Approval chain:[/] {string.Join(" → ", g.ApprovalChain)}"),
            new Markup($"[bold]Allowed consumers:[/] {string.Join(", ", g.AllowedConsumers)}"),
            new Markup($"[bold]Compliance:[/] {string.Join(", ", g.ComplianceFrameworks)}"),
            new Markup($"[bold]Retention:[/] {g.MaxDataRetentionDays} days")
        )).Header("[yellow]Governance Overlay[/]").Border(BoxBorder.Rounded);

        AnsiConsole.Write(govPanel);
    }
}, inspectRefArg);

rootCommand.AddCommand(inspectCommand);

// ═══════════════════════════════════════════════════════════
// AUDIT
// ═══════════════════════════════════════════════════════════

var auditCommand = new Command("audit", "Validate governance compliance before install");
var auditRefArg = new Argument<string>("reference", "OCI artifact reference");
var ceilingOption = new Option<string?>("--ceiling", "Override sensitivity ceiling for this check");
auditCommand.AddArgument(auditRefArg);
auditCommand.AddOption(ceilingOption);

auditCommand.SetHandler(async (string reference, string? ceiling) =>
{
    var config = LoadConfig();
    if (ceiling != null)
        config = config with { SensitivityCeiling = ceiling };

    AnsiConsole.MarkupLine($"[dim]Auditing governance for {reference}...[/]");
    AnsiConsole.MarkupLine($"[dim]Consumer: {config.ConsumerId} | Ceiling: {config.SensitivityCeiling}[/]\n");

    var record = await registry.FetchRecordAsync(reference);
    var gov = await registry.FetchGovernanceAsync(reference);

    if (record == null || gov == null)
    {
        AnsiConsole.MarkupLine("[red]Could not fetch OASF metadata.[/]");
        return;
    }

    var results = await governance.AuditAsync(config, record, gov, registry);

    var passed = results.All(r => r.Allowed);

    if (passed)
    {
        AnsiConsole.MarkupLine("[green]✓ All governance checks passed[/]");
        AnsiConsole.MarkupLine($"  Asset tier: [cyan]{gov.Governance.SensitivityTier}[/]");
        AnsiConsole.MarkupLine($"  Your ceiling: [cyan]{config.SensitivityCeiling}[/]");
        AnsiConsole.MarkupLine($"  Consumer: [cyan]{config.ConsumerId}[/]");
        AnsiConsole.MarkupLine($"  Frameworks: [cyan]{string.Join(", ", gov.Governance.ComplianceFrameworks)}[/]");
    }
    else
    {
        foreach (var r in results.Where(r => !r.Allowed))
        {
            AnsiConsole.MarkupLine($"[red]✗ {r.Reason}[/]");
            if (r.RequiredApprovals != null)
                AnsiConsole.MarkupLine($"  [yellow]Required approvals: {string.Join(" → ", r.RequiredApprovals)}[/]");
        }
    }
}, auditRefArg, ceilingOption);

rootCommand.AddCommand(auditCommand);

// ═══════════════════════════════════════════════════════════
// INSTALL
// ═══════════════════════════════════════════════════════════

var installCommand = new Command("install", "Pull and install an AI asset into a target runtime");
var installRefArg = new Argument<string>("reference", "OCI artifact reference");
var targetOption = new Option<string>("--target", "Install target runtime") { IsRequired = true };
targetOption.AddCompletions(TargetRegistry.Available.ToArray());
installCommand.AddArgument(installRefArg);
installCommand.AddOption(targetOption);

installCommand.SetHandler(async (string reference, string target) =>
{
    var config = LoadConfig();

    AnsiConsole.MarkupLine($"[bold]aria install[/] {reference} → {target}\n");

    // Step 1: Fetch metadata
    AnsiConsole.MarkupLine("[dim]1. Fetching OASF metadata...[/]");
    var record = await registry.FetchRecordAsync(reference);
    var gov = await registry.FetchGovernanceAsync(reference);

    if (record == null)
    {
        AnsiConsole.MarkupLine("[red]Could not fetch OASF Record.[/]");
        return;
    }

    AnsiConsole.MarkupLine($"   Asset: [cyan]{record.Name}[/] v{record.Version}");

    // Step 2: Governance check
    AnsiConsole.MarkupLine("[dim]2. Validating governance...[/]");
    if (gov != null)
    {
        var result = governance.ValidateInstall(config, gov);
        if (!result.Allowed)
        {
            AnsiConsole.MarkupLine($"[red]   ✗ {result.Reason}[/]");
            return;
        }
        AnsiConsole.MarkupLine($"   [green]✓[/] Sensitivity: {gov.Governance.SensitivityTier} ≤ {config.SensitivityCeiling}");
        AnsiConsole.MarkupLine($"   [green]✓[/] Consumer '{config.ConsumerId}' is authorized");
    }
    else
    {
        AnsiConsole.MarkupLine("   [yellow]⚠ No governance overlay found — installing without policy checks[/]");
    }

    // Step 3: Pull artifact
    AnsiConsole.MarkupLine("[dim]3. Pulling OCI artifact...[/]");
    var outputDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".aria", "cache", record.Name.Replace('/', '-'));
    var pulled = await registry.PullArtifactAsync(reference, outputDir);
    AnsiConsole.MarkupLine($"   Cached to: {outputDir}");

    // Step 4: Install to target
    AnsiConsole.MarkupLine($"[dim]4. Installing to {target}...[/]");
    var installTarget = TargetRegistry.Get(target);
    if (installTarget == null)
    {
        AnsiConsole.MarkupLine($"[red]Unknown target '{target}'. Available: {string.Join(", ", TargetRegistry.Available)}[/]");
        return;
    }

    var targetConfig = config.Targets.GetValueOrDefault(target) ?? new();
    var success = await installTarget.InstallAsync(pulled ?? outputDir, record, targetConfig);

    if (success)
    {
        AnsiConsole.MarkupLine($"\n[green]✓ Successfully installed {record.Name} v{record.Version} → {target}[/]");
    }
    else
    {
        AnsiConsole.MarkupLine($"\n[red]✗ Installation failed[/]");
    }
}, installRefArg, targetOption);

rootCommand.AddCommand(installCommand);

// ═══════════════════════════════════════════════════════════
// LIST
// ═══════════════════════════════════════════════════════════

var listCommand = new Command("list", "List installed AI assets");
var listTargetOption = new Option<string?>("--target", "Filter by install target");
listCommand.AddOption(listTargetOption);

listCommand.SetHandler(async (string? target) =>
{
    var installDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".aria", "installed.json");

    if (!File.Exists(installDir))
    {
        AnsiConsole.MarkupLine("[dim]No installed assets found.[/]");
        return;
    }

    var installed = JsonSerializer.Deserialize<List<InstalledAsset>>(
        await File.ReadAllTextAsync(installDir)) ?? [];

    if (target != null)
        installed = installed.Where(a => a.Target.Equals(target, StringComparison.OrdinalIgnoreCase)).ToList();

    if (installed.Count == 0)
    {
        AnsiConsole.MarkupLine("[dim]No installed assets found.[/]");
        return;
    }

    var table = new Table()
        .AddColumn("Name")
        .AddColumn("Version")
        .AddColumn("Type")
        .AddColumn("Target")
        .AddColumn("Sensitivity")
        .AddColumn("Installed")
        .Border(TableBorder.Rounded);

    foreach (var a in installed)
    {
        table.AddRow(
            $"[bold]{a.Name}[/]",
            a.Version,
            $"[cyan]{a.ModuleType}[/]",
            a.Target,
            a.SensitivityTier,
            a.InstalledAt.ToString("yyyy-MM-dd"));
    }

    AnsiConsole.Write(table);
}, listTargetOption);

rootCommand.AddCommand(listCommand);

// ═══════════════════════════════════════════════════════════
// INIT
// ═══════════════════════════════════════════════════════════

var initCommand = new Command("init", "Initialize aria configuration in ~/.aria/config.json");

initCommand.SetHandler(async () =>
{
    var dir = Path.GetDirectoryName(configPath)!;
    Directory.CreateDirectory(dir);

    if (File.Exists(configPath))
    {
        AnsiConsole.MarkupLine($"[yellow]Config already exists at {configPath}[/]");
        return;
    }

    var config = new AriaConfig
    {
        ConsumerId = "my-team",
        SensitivityCeiling = "confidential",
        Registries = ["ghcr.io/my-org/aria-assets"],
        Targets = new()
        {
            ["claude-desktop"] = new()
            {
                ConfigPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Library", "Application Support", "Claude", "claude_desktop_config.json")
            },
            ["agent-framework"] = new()
            {
                ProjectPath = "./src",
                A2AEndpoint = "https://agents.myorg.com"
            },
            ["vscode"] = new()
            {
                WorkspacePath = "./.vscode/mcp.json"
            }
        },
        Purview = new()
        {
            Account = "purview-myorg",
            TenantId = "your-tenant-id"
        }
    };

    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(configPath, json);

    AnsiConsole.MarkupLine($"[green]✓ Created config at {configPath}[/]");
    AnsiConsole.MarkupLine("[dim]Edit the file to set your consumer_id, registries, and target paths.[/]");
});

rootCommand.AddCommand(initCommand);

// ═══════════════════════════════════════════════════════════
// RUN
// ═══════════════════════════════════════════════════════════

return await rootCommand.InvokeAsync(args);
