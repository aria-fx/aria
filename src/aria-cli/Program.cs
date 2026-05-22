// ─────────────────────────────────────────────────────────────
// Program.cs
// ARIA Package Manager (aria) — CLI for discovering, validating,
// and installing OASF-governed AI assets from OCI registries.
// ─────────────────────────────────────────────────────────────

using System.CommandLine;
using System.Text.Json;
using Aria.Auth.Core.Models;
using Aria.Auth.Core.Services;
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
var identityProviderFactory = new IdentityProviderFactory(
[
    new EntraAuthService(),
    new OktaIdentityProvider(),
    new Auth0IdentityProvider()
]);
var accessPolicy = new AccessPolicyService(identityProviderFactory);

async Task<EffectiveAccessContext?> ResolveAccessAsync(AriaConfig config)
{
    try
    {
        return await accessPolicy.ResolveAsync(config);
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Authentication error: {Markup.Escape(ex.Message)}[/]");
        return null;
    }
}

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
var searchVerboseOption = new Option<bool>("--verbose", "Show per-registry discovery diagnostics");
searchCommand.AddOption(skillOption);
searchCommand.AddOption(domainOption);
searchCommand.AddOption(keywordOption);
searchCommand.AddOption(searchVerboseOption);

searchCommand.SetHandler(async (string? skill, string? domain, string? keyword, bool verbose) =>
{
    var config = LoadConfig();
    if (!config.Registries.Any(r => !string.IsNullOrWhiteSpace(r)))
    {
        AnsiConsole.MarkupLine("[red]No registries configured. Run `aria init` or update ~/.aria/config.json with at least one registry.[/]");
        return;
    }

    var searchResult = await registry.SearchDetailedAsync(skill, domain, keyword, config.Registries);
    var failedRegistries = searchResult.Diagnostics.Where(d => d.Error != null).ToList();

    if (verbose)
    {
        var diagnosticsTable = new Table()
            .AddColumn("Registry")
            .AddColumn("Status")
            .AddColumn("Results")
            .AddColumn("Details")
            .Border(TableBorder.Rounded);

        foreach (var diagnostic in searchResult.Diagnostics)
        {
            diagnosticsTable.AddRow(
                diagnostic.Registry,
                diagnostic.Error == null ? "[green]ok[/]" : "[yellow]error[/]",
                diagnostic.ResultCount.ToString(),
                diagnostic.Error == null ? "-" : Markup.Escape(diagnostic.Error));
        }

        AnsiConsole.Write(diagnosticsTable);
        AnsiConsole.WriteLine();
    }
    else if (failedRegistries.Count > 0)
    {
        AnsiConsole.MarkupLine("[yellow]Some registries were unavailable. Re-run with --verbose for per-registry diagnostics.[/]");
    }

    var results = searchResult.Records;

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
}, skillOption, domainOption, keywordOption, searchVerboseOption);

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
// WHOAMI
// ═══════════════════════════════════════════════════════════

var whoAmICommand = new Command("whoami", "Show resolved identity provider and effective access profile");

whoAmICommand.SetHandler(async () =>
{
    var config = LoadConfig();
    var access = await ResolveAccessAsync(config);
    if (access == null)
        return;

    AnsiConsole.MarkupLine("[bold]Effective access context[/]");
    AnsiConsole.MarkupLine($"Consumer: [cyan]{access.ConsumerId}[/]");
    AnsiConsole.MarkupLine($"Sensitivity ceiling: [cyan]{access.SensitivityCeiling}[/]");
    AnsiConsole.MarkupLine($"Purview roles: [cyan]{(access.PurviewRoles.Count > 0 ? string.Join(", ", access.PurviewRoles) : "none")}[/]");
    if (access.MatchedRules.Count > 0)
        AnsiConsole.MarkupLine($"Matched rules: [cyan]{string.Join(", ", access.MatchedRules)}[/]");

    if (access.Identity == null)
    {
        AnsiConsole.MarkupLine("Identity: [yellow]not resolved (provider disabled or no credential available)[/]");
        return;
    }

    AnsiConsole.MarkupLine("\n[bold]Identity[/]");
    AnsiConsole.MarkupLine($"Provider: [cyan]{access.Identity.Provider}[/]");
    AnsiConsole.MarkupLine($"Object ID: [cyan]{access.Identity.ObjectId}[/]");
    AnsiConsole.MarkupLine($"Tenant ID: [cyan]{access.Identity.TenantId}[/]");
    AnsiConsole.MarkupLine($"UPN: [cyan]{access.Identity.UserPrincipalName ?? "(n/a)"}[/]");
    AnsiConsole.MarkupLine($"Groups: [cyan]{(access.Identity.Groups.Count > 0 ? string.Join(", ", access.Identity.Groups) : "none")}[/]");
    AnsiConsole.MarkupLine($"Roles/scopes: [cyan]{(access.Identity.Roles.Count > 0 ? string.Join(", ", access.Identity.Roles) : "none")}[/]");
});

rootCommand.AddCommand(whoAmICommand);

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

    var access = await ResolveAccessAsync(config);
    if (access == null)
        return;

    AnsiConsole.MarkupLine($"[dim]Auditing governance for {reference}...[/]");
    AnsiConsole.MarkupLine($"[dim]Consumer: {access.ConsumerId} | Ceiling: {access.SensitivityCeiling}[/]\n");

    var record = await registry.FetchRecordAsync(reference);
    var gov = await registry.FetchGovernanceAsync(reference);

    if (record == null || gov == null)
    {
        AnsiConsole.MarkupLine("[red]Could not fetch OASF metadata.[/]");
        return;
    }

    var results = await governance.AuditAsync(config, record, gov, registry, access);

    var passed = results.All(r => r.Allowed);

    if (passed)
    {
        AnsiConsole.MarkupLine("[green]✓ All governance checks passed[/]");
        AnsiConsole.MarkupLine($"  Asset tier: [cyan]{gov.Governance.SensitivityTier}[/]");
        AnsiConsole.MarkupLine($"  Your ceiling: [cyan]{access.SensitivityCeiling}[/]");
        AnsiConsole.MarkupLine($"  Consumer: [cyan]{access.ConsumerId}[/]");
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
    var access = await ResolveAccessAsync(config);
    if (access == null)
        return;

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
        var result = governance.ValidateInstall(config, gov, access);
        if (!result.Allowed)
        {
            AnsiConsole.MarkupLine($"[red]   ✗ {result.Reason}[/]");
            return;
        }
        AnsiConsole.MarkupLine($"   [green]✓[/] Sensitivity: {gov.Governance.SensitivityTier} ≤ {access.SensitivityCeiling}");
        AnsiConsole.MarkupLine($"   [green]✓[/] Consumer '{access.ConsumerId}' is authorized");
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
            TenantId = "your-tenant-id",
            RequiredRolesBySensitivity = new(StringComparer.OrdinalIgnoreCase)
            {
                ["confidential"] = ["Data Reader"],
                ["highly_confidential"] = ["Data Curator"],
                ["restricted"] = ["Data Source Administrator"]
            }
        },
        Entra = new()
        {
            Enabled = true,
            TenantId = "your-tenant-id",
            Scopes = ["https://management.azure.com/.default"]
        },
        Auth = new()
        {
            Provider = "entra",
            EnableExperimentalProviders = false
        },
        Okta = new()
        {
            Enabled = false,
            Issuer = "https://your-org.okta.com/oauth2/default",
            ClientId = "your-okta-client-id",
            Scopes = ["openid", "profile", "groups"],
            AccessTokenEnvVar = "OKTA_ACCESS_TOKEN",
            AccessTokenFile = "~/.aria/okta-token.txt",
            TokenEndpoint = "https://your-org.okta.com/oauth2/default/v1/token",
            ClientSecretEnvVar = "OKTA_CLIENT_SECRET"
        },
        Auth0 = new()
        {
            Enabled = false,
            Domain = "your-tenant.us.auth0.com",
            ClientId = "your-auth0-client-id",
            Audience = "https://api.example.com",
            Scopes = ["openid", "profile", "read:assets"]
        },
        AccessRules =
        [
            new()
            {
                Name = "hr-readers",
                AnyEntraGroups = ["entra-group-hr-readers"],
                SensitivityCeiling = "confidential",
                PurviewRoles = ["Data Reader"]
            },
            new()
            {
                Name = "governance-admins",
                AnyEntraRoles = ["Governance.Admin"],
                SensitivityCeiling = "restricted",
                PurviewRoles = ["Data Source Administrator", "Data Curator"]
            }
        ]
    };

    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(configPath, json);

    AnsiConsole.MarkupLine($"[green]✓ Created config at {configPath}[/]");
    AnsiConsole.MarkupLine("[dim]Edit the file to set auth provider settings, access_rules, registries, and target paths.[/]");
});

rootCommand.AddCommand(initCommand);

// ═══════════════════════════════════════════════════════════
// RUN
// ═══════════════════════════════════════════════════════════

return await rootCommand.InvokeAsync(args);
