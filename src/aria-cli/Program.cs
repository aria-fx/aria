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
    return new AriaConfig { Registries = [] };
}

async Task SaveConfigAsync(AriaConfig config)
{
    var dir = Path.GetDirectoryName(configPath)!;
    Directory.CreateDirectory(dir);
    var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
    await File.WriteAllTextAsync(configPath, json);
}

bool TryNormalizeRegistry(string candidate, out string normalized, out string error)
{
    normalized = string.Empty;
    error = string.Empty;

    if (string.IsNullOrWhiteSpace(candidate))
    {
        error = "Registry value cannot be empty.";
        return false;
    }

    var trimmed = candidate.Trim();

    if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri))
    {
        if (absoluteUri.IsFile)
        {
            normalized = Path.GetFullPath(absoluteUri.LocalPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return true;
        }

        if (string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            var absolutePath = absoluteUri.AbsolutePath.TrimEnd('/');
            var host = absoluteUri.Host.ToLowerInvariant();
            var port = absoluteUri.IsDefaultPort ? "" : $":{absoluteUri.Port}";
            normalized = $"{host}{port}{absolutePath}";
            return true;
        }

        error = $"Unsupported registry URL scheme '{absoluteUri.Scheme}'.";
        return false;
    }

    if (trimmed.Contains("://", StringComparison.Ordinal))
    {
        error = "Registry URL is invalid.";
        return false;
    }

    if (trimmed.StartsWith("~", StringComparison.Ordinal) || Path.IsPathRooted(trimmed) || trimmed.StartsWith(".", StringComparison.Ordinal))
    {
        try
        {
            normalized = Path.GetFullPath(PathHelper.ExpandTildePath(trimmed))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return true;
        }
        catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or NotSupportedException or PathTooLongException)
        {
            error = $"Registry path is invalid: {ex.Message}";
            return false;
        }
    }

    if (trimmed.Contains(' '))
    {
        error = "Registry URL is invalid.";
        return false;
    }

    if (!trimmed.Contains('/'))
    {
        error = "Registry URL is invalid. Expected host/path format.";
        return false;
    }

    if (!Uri.TryCreate($"https://{trimmed}", UriKind.Absolute, out var inferred))
    {
        error = "Registry URL is invalid.";
        return false;
    }

    var host = inferred.Host.ToLowerInvariant();
    var port = inferred.IsDefaultPort ? "" : $":{inferred.Port}";
    if (!host.Contains('.') && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
    {
        error = "Registry URL is invalid. Host name is missing.";
        return false;
    }

    normalized = $"{host}{port}{inferred.AbsolutePath}".TrimEnd('/');
    return true;
}

AriaConfig NormalizeRegistryConfig(AriaConfig config)
{
    var registries = config.Registries
        .Where(r => !string.IsNullOrWhiteSpace(r))
        .Select(r => TryNormalizeRegistry(r, out var normalized, out _) ? normalized : r.Trim().TrimEnd('/'))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    var registryPolicies = new Dictionary<string, RegistrySourcePolicyConfig>(StringComparer.OrdinalIgnoreCase);
    foreach (var policy in config.RegistryPolicies)
    {
        var key = TryNormalizeRegistry(policy.Key, out var normalized, out _) ? normalized : policy.Key.Trim().TrimEnd('/');
        if (registries.Contains(key, StringComparer.OrdinalIgnoreCase) && !registryPolicies.ContainsKey(key))
            registryPolicies[key] = policy.Value;
    }

    var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var alias in config.RegistryAliases)
    {
        var aliasName = alias.Key.Trim();
        if (string.IsNullOrWhiteSpace(aliasName))
            continue;

        if (!TryNormalizeRegistry(alias.Value, out var normalized, out _))
            continue;

        var matching = registries.FirstOrDefault(r => string.Equals(r, normalized, StringComparison.OrdinalIgnoreCase));
        if (matching != null)
            aliases[aliasName] = matching;
    }

    string? defaultRegistry = null;
    if (!string.IsNullOrWhiteSpace(config.DefaultRegistry) &&
        TryNormalizeRegistry(config.DefaultRegistry, out var defaultNormalized, out _) &&
        registries.Any(r => string.Equals(r, defaultNormalized, StringComparison.OrdinalIgnoreCase)))
    {
        defaultRegistry = registries.First(r => string.Equals(r, defaultNormalized, StringComparison.OrdinalIgnoreCase));
    }
    else if (registries.Count > 0)
    {
        defaultRegistry = registries[0];
    }

    return config with
    {
        Registries = registries,
        RegistryPolicies = registryPolicies,
        RegistryAliases = aliases,
        DefaultRegistry = defaultRegistry
    };
}

bool TryResolveRegistryIdentifier(AriaConfig config, string identifier, out string registry, out string error)
{
    registry = string.Empty;
    error = string.Empty;

    if (config.RegistryAliases.TryGetValue(identifier.Trim(), out var aliased))
    {
        registry = aliased;
        return true;
    }

    if (!TryNormalizeRegistry(identifier, out var normalized, out var normalizeError))
    {
        error = normalizeError;
        return false;
    }

    var match = config.Registries.FirstOrDefault(r => string.Equals(r, normalized, StringComparison.OrdinalIgnoreCase));
    if (match == null)
    {
        error = $"Registry '{identifier}' was not found.";
        return false;
    }

    registry = match;
    return true;
}

string? ResolveLocalRegistryPath(string registryValue)
{
    if (Uri.TryCreate(registryValue, UriKind.Absolute, out var uri) && uri.IsFile)
        return uri.LocalPath;

    if (Directory.Exists(registryValue))
        return registryValue;

    return null;
}

void FailRegistryCommand(string message, bool emitMessage = true)
{
    if (emitMessage && !string.IsNullOrWhiteSpace(message))
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(message)}[/]");
    Environment.Exit(1);
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

RegistrySourcePolicyConfig ResolveSourcePolicy(AriaConfig config, string registry) =>
    RegistryPolicyEvaluator.ResolvePolicy(config, registry);

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
    var config = NormalizeRegistryConfig(LoadConfig());
    if (!config.Registries.Any(r => !string.IsNullOrWhiteSpace(r)))
    {
        AnsiConsole.MarkupLine("[red]No registries configured. Run `aria init` or update ~/.aria/config.json with at least one registry.[/]");
        return;
    }

    var searchResult = await registry.SearchDetailedAsync(
        skill,
        domain,
        keyword,
        config.Registries,
        config.RegistryPolicies);
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
                Markup.Escape(diagnostic.Registry),
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
        .AddColumn("Source")
        .AddColumn("Governance")
        .AddColumn("Type")
        .AddColumn("Skills")
        .AddColumn("Description")
        .Border(TableBorder.Rounded);

    foreach (var selected in searchResult.SelectedAssets)
    {
        var r = selected.Record;
        var moduleType = r.Modules.FirstOrDefault()?.Type ?? "agent";
        var skills = string.Join(", ", r.Skills.Select(s => s.Name.Split('/').Last()));
        var governanceMarkup = selected.GovernanceState switch
        {
            RegistryGovernanceStates.Governed => "[green]governed[/]",
            RegistryGovernanceStates.Ungoverned => "[yellow]ungoverned[/]",
            _ => "[red]governance_unreachable[/]"
        };

        table.AddRow(
            $"[bold]{r.Name}[/]",
            r.Version,
            Markup.Escape(selected.Registry),
            governanceMarkup,
            $"[cyan]{moduleType}[/]",
            skills,
            r.Description.Length > 50 ? r.Description[..50] + "..." : r.Description);

        if (selected.IsAmbiguous && selected.ResolutionReason != null)
            AnsiConsole.MarkupLine($"[yellow]⚠ {Markup.Escape(r.Name)}: {Markup.Escape(selected.ResolutionReason)}[/]");
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
    var config = NormalizeRegistryConfig(LoadConfig());
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
    var config = NormalizeRegistryConfig(LoadConfig());
    if (ceiling != null)
        config = config with { SensitivityCeiling = ceiling };

    var access = await ResolveAccessAsync(config);
    if (access == null)
        return;

    AnsiConsole.MarkupLine($"[dim]Auditing governance for {reference}...[/]");
    AnsiConsole.MarkupLine($"[dim]Consumer: {access.ConsumerId} | Ceiling: {access.SensitivityCeiling}[/]\n");

    var record = await registry.FetchRecordAsync(reference);
    var sourceRegistry = RegistryPolicyEvaluator.ResolveRegistryFromReference(reference, config.Registries) ?? "<unspecified>";
    var sourcePolicy = ResolveSourcePolicy(config, sourceRegistry);
    var governanceFetch = await registry.FetchGovernanceWithStateAsync(reference);
    var gov = governanceFetch.Overlay;
    var sourcePolicyDecision = RegistryPolicyEvaluator.EvaluateInstallPolicy(
        sourcePolicy,
        governanceFetch.GovernanceState,
        access.SensitivityCeiling);

    if (record == null)
    {
        AnsiConsole.MarkupLine("[red]Could not fetch OASF Record.[/]");
        return;
    }

    if (!sourcePolicyDecision.Allowed)
    {
        AnsiConsole.MarkupLine($"[red]✗ {sourcePolicyDecision.Reason}[/]");
        AnsiConsole.MarkupLine($"[dim]Source: {sourceRegistry} | governance: {sourcePolicyDecision.GovernanceState}[/]");
        return;
    }

    var results = gov != null
        ? await governance.AuditAsync(config, record, gov, registry, access)
        : [new GovernanceResult(true, sourcePolicyDecision.Reason)];

    var passed = results.All(r => r.Allowed);

    if (passed)
    {
        AnsiConsole.MarkupLine(gov != null
            ? "[green]✓ All governance checks passed[/]"
            : "[yellow]✓ Registry policy check passed; governance overlay checks were skipped[/]");
        AnsiConsole.MarkupLine($"  Source: [cyan]{sourceRegistry}[/]");
        AnsiConsole.MarkupLine($"  Governance state: [cyan]{sourcePolicyDecision.GovernanceState}[/]");
        if (gov != null)
            AnsiConsole.MarkupLine($"  Asset tier: [cyan]{gov.Governance.SensitivityTier}[/]");
        else
            AnsiConsole.MarkupLine($"  Policy reason: [cyan]{Markup.Escape(sourcePolicyDecision.Reason)}[/]");
        AnsiConsole.MarkupLine($"  Your ceiling: [cyan]{access.SensitivityCeiling}[/]");
        AnsiConsole.MarkupLine($"  Consumer: [cyan]{access.ConsumerId}[/]");
        if (gov != null)
            AnsiConsole.MarkupLine($"  Frameworks: [cyan]{string.Join(", ", gov.Governance.ComplianceFrameworks)}[/]");
        if (!string.IsNullOrWhiteSpace(governanceFetch.Error))
            AnsiConsole.MarkupLine($"  [yellow]Governance fetch detail: {Markup.Escape(governanceFetch.Error)}[/]");
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
    var config = NormalizeRegistryConfig(LoadConfig());
    var access = await ResolveAccessAsync(config);
    if (access == null)
        return;

    AnsiConsole.MarkupLine($"[bold]aria install[/] {reference} → {target}\n");

    // Step 1: Fetch metadata
    AnsiConsole.MarkupLine("[dim]1. Fetching OASF metadata...[/]");
    var record = await registry.FetchRecordAsync(reference);
    var sourceRegistry = RegistryPolicyEvaluator.ResolveRegistryFromReference(reference, config.Registries) ?? "<unspecified>";
    var sourcePolicy = ResolveSourcePolicy(config, sourceRegistry);
    var governanceFetch = await registry.FetchGovernanceWithStateAsync(reference);
    var gov = governanceFetch.Overlay;

    if (record == null)
    {
        AnsiConsole.MarkupLine("[red]Could not fetch OASF Record.[/]");
        return;
    }

    AnsiConsole.MarkupLine($"   Asset: [cyan]{record.Name}[/] v{record.Version}");

    // Step 2: Governance check
    AnsiConsole.MarkupLine("[dim]2. Validating governance...[/]");
    var sourcePolicyDecision = RegistryPolicyEvaluator.EvaluateInstallPolicy(
        sourcePolicy,
        governanceFetch.GovernanceState,
        access.SensitivityCeiling);
    if (!sourcePolicyDecision.Allowed)
    {
        AnsiConsole.MarkupLine($"[red]   ✗ {sourcePolicyDecision.Reason}[/]");
        AnsiConsole.MarkupLine($"   [dim]Source: {sourceRegistry} | governance: {sourcePolicyDecision.GovernanceState}[/]");
        return;
    }

    if (gov != null)
    {
        var result = governance.ValidateInstall(config, gov, access);
        if (!result.Allowed)
        {
            AnsiConsole.MarkupLine($"[red]   ✗ {result.Reason}[/]");
            return;
        }
        AnsiConsole.MarkupLine($"   [green]✓[/] Source: {sourceRegistry} ({sourcePolicy.TrustTier})");
        AnsiConsole.MarkupLine($"   [green]✓[/] Governance state: {sourcePolicyDecision.GovernanceState}");
        AnsiConsole.MarkupLine($"   [green]✓[/] Policy reason: {Markup.Escape(sourcePolicyDecision.Reason)}");
        AnsiConsole.MarkupLine($"   [green]✓[/] Sensitivity: {gov.Governance.SensitivityTier} ≤ {access.SensitivityCeiling}");
        AnsiConsole.MarkupLine($"   [green]✓[/] Consumer '{access.ConsumerId}' is authorized");
    }
    else
    {
        AnsiConsole.MarkupLine($"   [green]✓[/] Source: {sourceRegistry} ({sourcePolicy.TrustTier})");
        AnsiConsole.MarkupLine($"   [green]✓[/] Governance state: {sourcePolicyDecision.GovernanceState}");
        AnsiConsole.MarkupLine($"   [yellow]⚠ Registry policy allowed install: {Markup.Escape(sourcePolicyDecision.Reason)}[/]");
        AnsiConsole.MarkupLine("   [yellow]⚠ Governance overlay not available; overlay validation was skipped[/]");
        AnsiConsole.MarkupLine("   [yellow]⚠ Sensitivity and consumer authorization checks were not performed[/]");
        if (!string.IsNullOrWhiteSpace(governanceFetch.Error))
            AnsiConsole.MarkupLine($"   [yellow]⚠ Governance fetch detail: {Markup.Escape(governanceFetch.Error)}[/]");
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
        DefaultRegistry = "ghcr.io/my-org/aria-assets",
        RegistryAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            ["primary"] = "ghcr.io/my-org/aria-assets"
        },
        RegistryPolicies = new(StringComparer.OrdinalIgnoreCase)
        {
            ["ghcr.io/my-org/aria-assets"] = new()
            {
                TrustTier = "internal_governed",
                RequireGovernanceOverlay = true,
                Priority = 100
            }
        },
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
// REGISTRY
// ═══════════════════════════════════════════════════════════

var registryCommand = new Command("registry", "Manage configured registry sources");

var registryListCommand = new Command("list", "List configured registries");
var registryListJsonOption = new Option<bool>("--json", "Output machine-readable JSON");
registryListCommand.AddOption(registryListJsonOption);
registryListCommand.SetHandler(async (bool asJson) =>
{
    var config = NormalizeRegistryConfig(LoadConfig());
    await SaveConfigAsync(config);

    var aliasesByRegistry = config.RegistryAliases
        .GroupBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            g => g.Key,
            g => g.Select(kvp => kvp.Key).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList(),
            StringComparer.OrdinalIgnoreCase);

    var items = config.Registries
        .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
        .Select(r => new
        {
            name = aliasesByRegistry.TryGetValue(r, out var aliases) ? aliases.FirstOrDefault() : null,
            url = r,
            isDefault = string.Equals(config.DefaultRegistry, r, StringComparison.OrdinalIgnoreCase)
        })
        .ToList();

    if (asJson)
    {
        var json = JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true });
        AnsiConsole.WriteLine(json);
        return;
    }

    if (items.Count == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No registries configured.[/]");
        return;
    }

    foreach (var item in items)
    {
        var marker = item.isDefault ? "[green]*[/]" : " ";
        var name = item.name is null ? "" : $"[cyan]{Markup.Escape(item.name)}[/] -> ";
        AnsiConsole.MarkupLine($"{marker} {name}{Markup.Escape(item.url)}");
    }
}, registryListJsonOption);
registryCommand.AddCommand(registryListCommand);

var registryAddCommand = new Command("add", "Add a registry source");
var registryAddUrlArg = new Argument<string>("url", "Registry URL/path");
var registryAddNameOption = new Option<string?>("--name", "Optional registry alias");
var registryAddDefaultOption = new Option<bool>("--default", "Set as the default registry");
registryAddCommand.AddArgument(registryAddUrlArg);
registryAddCommand.AddOption(registryAddNameOption);
registryAddCommand.AddOption(registryAddDefaultOption);
registryAddCommand.SetHandler(async (string url, string? name, bool setDefault) =>
{
    var config = NormalizeRegistryConfig(LoadConfig());
    if (!TryNormalizeRegistry(url, out var normalizedUrl, out var normalizeError))
    {
        FailRegistryCommand(normalizeError);
        return;
    }

    if (config.Registries.Any(r => string.Equals(r, normalizedUrl, StringComparison.OrdinalIgnoreCase)))
    {
        FailRegistryCommand($"Registry '{normalizedUrl}' is already configured.");
        return;
    }

    var nextRegistries = config.Registries.Append(normalizedUrl)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    var nextAliases = new Dictionary<string, string>(config.RegistryAliases, StringComparer.OrdinalIgnoreCase);

    if (!string.IsNullOrWhiteSpace(name))
    {
        var aliasName = name.Trim();
        if (nextAliases.TryGetValue(aliasName, out var existingAliasUrl) &&
            !string.Equals(existingAliasUrl, normalizedUrl, StringComparison.OrdinalIgnoreCase))
        {
            FailRegistryCommand($"Alias '{aliasName}' is already assigned to '{existingAliasUrl}'.");
            return;
        }

        nextAliases[aliasName] = normalizedUrl;
    }

    var updated = config with
    {
        Registries = nextRegistries,
        RegistryAliases = nextAliases,
        DefaultRegistry = setDefault || string.IsNullOrWhiteSpace(config.DefaultRegistry)
            ? normalizedUrl
            : config.DefaultRegistry
    };

    await SaveConfigAsync(updated);
    AnsiConsole.MarkupLine($"[green]Added registry[/] {Markup.Escape(normalizedUrl)}");
}, registryAddUrlArg, registryAddNameOption, registryAddDefaultOption);
registryCommand.AddCommand(registryAddCommand);

var registryRemoveCommand = new Command("remove", "Remove a registry by alias or URL");
var registryRemoveArg = new Argument<string>("name-or-url", "Registry alias or URL/path");
registryRemoveCommand.AddArgument(registryRemoveArg);
registryRemoveCommand.SetHandler(async (string nameOrUrl) =>
{
    var config = NormalizeRegistryConfig(LoadConfig());
    if (!TryResolveRegistryIdentifier(config, nameOrUrl, out var existingRegistry, out var resolveError))
    {
        FailRegistryCommand(resolveError);
        return;
    }

    var registries = config.Registries
        .Where(r => !string.Equals(r, existingRegistry, StringComparison.OrdinalIgnoreCase))
        .ToList();
    var policies = config.RegistryPolicies
        .Where(kvp => !string.Equals(kvp.Key, existingRegistry, StringComparison.OrdinalIgnoreCase))
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    var aliases = config.RegistryAliases
        .Where(kvp => !string.Equals(kvp.Value, existingRegistry, StringComparison.OrdinalIgnoreCase) &&
                      !string.Equals(kvp.Key, nameOrUrl, StringComparison.OrdinalIgnoreCase))
        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    var defaultRegistry = string.Equals(config.DefaultRegistry, existingRegistry, StringComparison.OrdinalIgnoreCase)
        ? registries.FirstOrDefault()
        : config.DefaultRegistry;

    await SaveConfigAsync(config with
    {
        Registries = registries,
        RegistryPolicies = policies,
        RegistryAliases = aliases,
        DefaultRegistry = defaultRegistry
    });
    AnsiConsole.MarkupLine($"[green]Removed registry[/] {Markup.Escape(existingRegistry)}");
}, registryRemoveArg);
registryCommand.AddCommand(registryRemoveCommand);

var registryUpdateCommand = new Command("update", "Update a registry URL by alias or current URL");
var registryUpdateIdentifierArg = new Argument<string>("name-or-url", "Registry alias or URL/path");
var registryUpdateUrlOption = new Option<string>("--url", "New registry URL/path") { IsRequired = true };
registryUpdateCommand.AddArgument(registryUpdateIdentifierArg);
registryUpdateCommand.AddOption(registryUpdateUrlOption);
registryUpdateCommand.SetHandler(async (string nameOrUrl, string newUrl) =>
{
    var config = NormalizeRegistryConfig(LoadConfig());
    if (!TryResolveRegistryIdentifier(config, nameOrUrl, out var existingRegistry, out var resolveError))
    {
        FailRegistryCommand(resolveError);
        return;
    }

    if (!TryNormalizeRegistry(newUrl, out var normalizedUrl, out var normalizeError))
    {
        FailRegistryCommand(normalizeError);
        return;
    }

    if (config.Registries.Any(r => !string.Equals(r, existingRegistry, StringComparison.OrdinalIgnoreCase) &&
                                   string.Equals(r, normalizedUrl, StringComparison.OrdinalIgnoreCase)))
    {
        FailRegistryCommand($"Registry '{normalizedUrl}' is already configured.");
        return;
    }

    var registries = config.Registries
        .Select(r => string.Equals(r, existingRegistry, StringComparison.OrdinalIgnoreCase) ? normalizedUrl : r)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    var policies = new Dictionary<string, RegistrySourcePolicyConfig>(StringComparer.OrdinalIgnoreCase);
    foreach (var entry in config.RegistryPolicies)
    {
        var key = string.Equals(entry.Key, existingRegistry, StringComparison.OrdinalIgnoreCase) ? normalizedUrl : entry.Key;
        if (!policies.ContainsKey(key))
            policies[key] = entry.Value;
    }
    var aliases = config.RegistryAliases.ToDictionary(
        kvp => kvp.Key,
        kvp => string.Equals(kvp.Value, existingRegistry, StringComparison.OrdinalIgnoreCase) ? normalizedUrl : kvp.Value,
        StringComparer.OrdinalIgnoreCase);
    var defaultRegistry = string.Equals(config.DefaultRegistry, existingRegistry, StringComparison.OrdinalIgnoreCase)
        ? normalizedUrl
        : config.DefaultRegistry;

    await SaveConfigAsync(config with
    {
        Registries = registries,
        RegistryPolicies = policies,
        RegistryAliases = aliases,
        DefaultRegistry = defaultRegistry
    });

    AnsiConsole.MarkupLine($"[green]Updated registry[/] {Markup.Escape(existingRegistry)} [dim]->[/] {Markup.Escape(normalizedUrl)}");
}, registryUpdateIdentifierArg, registryUpdateUrlOption);
registryCommand.AddCommand(registryUpdateCommand);

var registrySetDefaultCommand = new Command("set-default", "Set default registry by alias or URL");
var registrySetDefaultArg = new Argument<string>("name-or-url", "Registry alias or URL/path");
registrySetDefaultCommand.AddArgument(registrySetDefaultArg);
registrySetDefaultCommand.SetHandler(async (string nameOrUrl) =>
{
    var config = NormalizeRegistryConfig(LoadConfig());
    if (!TryResolveRegistryIdentifier(config, nameOrUrl, out var registryToSet, out var resolveError))
    {
        FailRegistryCommand(resolveError);
        return;
    }

    await SaveConfigAsync(config with { DefaultRegistry = registryToSet });
    AnsiConsole.MarkupLine($"[green]Default registry set to[/] {Markup.Escape(registryToSet)}");
}, registrySetDefaultArg);
registryCommand.AddCommand(registrySetDefaultCommand);

var registryValidateCommand = new Command("validate", "Validate configured registries");
var registryValidateArg = new Argument<string?>("name-or-url", () => null, "Registry alias or URL/path (optional)");
var registryValidateJsonOption = new Option<bool>("--json", "Output machine-readable JSON");
registryValidateCommand.AddArgument(registryValidateArg);
registryValidateCommand.AddOption(registryValidateJsonOption);
registryValidateCommand.SetHandler(async (string? nameOrUrl, bool asJson) =>
{
    var config = NormalizeRegistryConfig(LoadConfig());
    await SaveConfigAsync(config);

    List<string> targets;
    if (string.IsNullOrWhiteSpace(nameOrUrl))
    {
        targets = config.Registries.OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
    }
    else
    {
        if (!TryResolveRegistryIdentifier(config, nameOrUrl, out var resolved, out var resolveError))
        {
            FailRegistryCommand(resolveError, emitMessage: !asJson);
            return;
        }
        targets = [resolved];
    }

    var aliasLookup = config.RegistryAliases
        .GroupBy(kvp => kvp.Value, StringComparer.OrdinalIgnoreCase)
        .ToDictionary(
            g => g.Key,
            g => g.Select(kvp => kvp.Key).OrderBy(v => v, StringComparer.OrdinalIgnoreCase).FirstOrDefault(),
            StringComparer.OrdinalIgnoreCase);

    var results = targets.Select(target =>
    {
        var localPath = ResolveLocalRegistryPath(target);
        if (localPath != null)
        {
            var exists = Directory.Exists(localPath);
            return new
            {
                name = aliasLookup.GetValueOrDefault(target),
                url = target,
                ok = exists,
                message = exists ? "Registry path is reachable." : "Registry path does not exist."
            };
        }

        return new
        {
            name = aliasLookup.GetValueOrDefault(target),
            url = target,
            ok = true,
            message = "Registry URL format is valid."
        };
    }).ToList();

    if (asJson)
    {
        var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true });
        AnsiConsole.WriteLine(json);
    }
    else
    {
        foreach (var result in results)
        {
            var status = result.ok ? "[green]ok[/]" : "[red]error[/]";
            var name = string.IsNullOrWhiteSpace(result.name) ? "" : $"{Markup.Escape(result.name)} -> ";
            AnsiConsole.MarkupLine($"{status} {name}{Markup.Escape(result.url)} [dim]- {Markup.Escape(result.message)}[/]");
        }
    }

    if (results.Any(r => !r.ok))
        FailRegistryCommand("One or more registries failed validation.", emitMessage: !asJson);
}, registryValidateArg, registryValidateJsonOption);
registryCommand.AddCommand(registryValidateCommand);

rootCommand.AddCommand(registryCommand);

// ═══════════════════════════════════════════════════════════
// RUN
// ═══════════════════════════════════════════════════════════

return await rootCommand.InvokeAsync(args);
